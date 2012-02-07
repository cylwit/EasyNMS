using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using System.Diagnostics;
using EasyNms.EndPoints;
using System.Configuration;
using EasyNms.Configuration;
using System.Reflection;
using System.ComponentModel;
using System.Threading;

namespace EasyNms
{
    public class NmsConnectionPool : IDisposable, INmsConnection
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private static INmsConnection instance;
        private static object instanceLock = new object();

        public event EventHandler<NmsConnectionEventArgs> ConnectionInterrupted;
        public event EventHandler<NmsConnectionEventArgs> ConnectionResumed;
        public event EventHandler<NmsConnectionEventArgs> ConnectionAvailable;

        #region Fields

        private object getNextConnectionLock = new object();
        private object getConnectionWithTimeoutLock = new object();

        private IConnectionFactory connectionFactory;
        private NmsConnectionPoolSettings settings;
        private List<NmsPooledConnection> connections;
        private List<NmsConnection> connectionsForCleanup;
        private bool isDisposed;
        private int connectionIndex;
        private INmsEndPointManager endpointManager;

        private Timer statusTimer;
        private Timer recoveryTimer;
        private Timer cleanupTimer;
        private bool isRecovering, isCleaningUp;
        private AutoResetEvent startEvent = new AutoResetEvent(false);
        private Thread startThread = null;
        private volatile int pendingConnections;

        #endregion

        #region Properties

        public bool IsStarted { get; private set; }

        #endregion
        #region Properties [static]

        public static INmsConnection Instance
        {
            get
            {
                if (instance == null)
                    throw new InvalidOperationException("Cannot access the global NmsConnectionPool instance without first calling StartGlobalInstance().");
                return instance;
            }
            private set
            {
                instance = value;
            }
        }

        #endregion

        #region Constructors

        public NmsConnectionPool(IConnectionFactory connectionFactory, NmsConnectionPoolSettings settings)
            : this()
        {
            this.Init();

            this.connectionFactory = connectionFactory;
            this.settings = settings;
            
            this.endpointManager = new RoundRobinNmsEndPointManager();
            foreach (var ep in settings.EndPoints)
                this.endpointManager.AddEndPoint(ep);
        }

        public NmsConnectionPool()
        {
            this.Init();

            var config = (EasyNmsSection)ConfigurationManager.GetSection("easyNms");
            this.settings = new NmsConnectionPoolSettings()
            {
                ConnectionCount = config.ConnectionPool.NumberOfConnections,
                MaximumSessionsPerConnection = config.ConnectionPool.MaxSessionsPerConnection,
                MinimumSessionsPerConnection = config.ConnectionPool.MinSessionsPerConnection,
                AcknowledgementMode = config.ConnectionPool.AcknowledgementMode,
                AutoGrowSessions = config.ConnectionPool.AutoGrowSessions
            };

            Type epManagerType = null;
            try
            {
                epManagerType = Type.GetType(config.ConnectionPool.EndPointManager.Type);
                if (epManagerType.GetInterface("INmsEndPointManager") == null)
                    throw new ConfigurationErrorsException("The type '" + config.ConnectionPool.EndPointManager.Type + "' does not implement INmsEndPointManager.");
            }
            catch (Exception)
            {
                throw new ConfigurationErrorsException("Could not find type '" + config.ConnectionPool.EndPointManager.Type + "'.");
            }

            endpointManager = (INmsEndPointManager)Activator.CreateInstance(epManagerType);
            if (config.ConnectionPool.EndPointManager.Properties != null && config.ConnectionPool.EndPointManager.Properties.Count > 0)
            {
                foreach (var pInfo in epManagerType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    foreach (PropertyElement prop in config.ConnectionPool.EndPointManager.Properties)
                    {
                        if (prop.Name == pInfo.Name)
                        {
                            var conv = TypeDescriptor.GetConverter(pInfo.PropertyType);
                            if (conv.CanConvertFrom(typeof(string)))
                            {
                                var value = conv.ConvertFrom(prop.Value);
                                pInfo.SetValue(endpointManager, value, null);
                            }
                        }
                    }
                }
            }

            foreach (EndPointElement ep in config.ConnectionPool.EndPoints)
            {
                NmsCredentials creds = null;
                if (ep.Credentials != null && (ep.Credentials.Username != string.Empty || ep.Credentials.Password != string.Empty))
                    creds = new NmsCredentials(ep.Credentials.Username, ep.Credentials.Password);

                var newep = new NmsEndPoint(ep.Uri, creds);
                endpointManager.AddEndPoint(newep);
            }
        }

        #endregion

        #region Methods [public]

        /// <summary>
        /// Starts the connection pool and all of its connections.
        /// </summary>
        void INmsConnection.Start()
        {
            this.AssertNotDisposed();
            new Thread(new ThreadStart(this.InnerStart)).Start();
        }

        public NmsConnectionPool Start()
        {
            this.AssertNotDisposed();
            new Thread(new ThreadStart(this.InnerStart)).Start();
            return this;
        }

        public NmsConnectionPool StartSync(int timeout)
        {
            this.AssertNotDisposed();
            this.startThread = new Thread(new ThreadStart(this.InnerStart));
            this.startThread.Start();
            this.startEvent.WaitOne(timeout);
            if (this.connections.Count == 0)
            {
                this.startThread.Abort();
                this.InnerStop();
                throw new TimeoutException("The connection pool could not be started within the specified timeout period.");
            }
            return this;
        }

        /// <summary>
        /// Stops the connection pool and all of its connections.
        /// </summary>
        public void Stop()
        {
            this.AssertNotDisposed();

            lock (this)
                this.InnerStop();

            this.statusTimer.Dispose();
            this.recoveryTimer.Dispose();
        }

        [Obsolete("Use GetConnection() instead.", true)]
        public NmsConnection BorrowConnection()
        {
            this.AssertNotDisposed();
            this.AssertHasConnections();

            var connection = this.GetNextConnection();
            return connection;
        }

        /// <summary>
        /// Attempts to get a connection from the pool, throwing an exception if no active connections are available.
        /// </summary>
        /// <returns>Returns an NmsConnection instance of a connection from the pool.</returns>
        public NmsConnection GetConnection()
        {
            this.AssertNotDisposed();
            this.AssertHasConnections();

            var connection = this.GetNextConnection();
            //this.connectionReferenceCounters[connection]++;
            return connection;
        }

        public NmsConnection GetConnection(int timeout)
        {
            var sw = Stopwatch.StartNew();
            lock (this.getConnectionWithTimeoutLock)
            {
                NmsConnection connection = null;
                do
                {
                    try
                    {
                        if (this.connections.Count == 0)
                            continue;
                        connection = this.GetConnection();
                    }
                    catch (Exception)
                    {
                    }
                    Thread.Sleep(5);
                }
                while (connection == null && sw.ElapsedMilliseconds < timeout);
                sw.Stop();

                if (connection == null)
                    throw new TimeoutException("Could not retrieve a new connection within the specified timeout.");
                return connection;
            }
        }

        [Obsolete("Not needed", true)]
        public void ReturnConnection(NmsConnection connection)
        {
            this.AssertNotDisposed();
            this.AssertIsStarted();

            var pooledConnection = connection as NmsPooledConnection;

            if (pooledConnection == null)
                throw new InvalidOperationException("Attempted to return a non-pooled connection to the connection pool.");
        }

        public INmsConnection AsINmsConnection()
        {
            return (INmsConnection)this;
        }

        #endregion
        #region Methods [private]

        private void InnerStart()
        {
            if (this.IsStarted)
                return;

            log.Info("*** The NMS connection pool is starting ***");
            log.Info("Creating {0} new connections...", this.settings.ConnectionCount);
            for (int i = 0; i < this.settings.ConnectionCount; i++)
            {
                try
                {
                    //this.NewConnection();
                    new Thread(new ThreadStart(this.VoidNewConnection)).Start();
                }
                catch (Exception ex)
                {
                    log.Error("[{0}] An error occurred while creating the connection: {1}", i, ex.Message);
                }
            }

            this.IsStarted = true;

            this.statusTimer = new Timer((o) =>
            {
                var sb = new StringBuilder();
                var currentConnections = this.connections.ToArray();
                foreach (var ep in currentConnections.Select(x => x.endPoint).Distinct())
                    sb.AppendFormat("{0} ({1}),", ep.Uri, currentConnections.Where(x => x.endPoint == ep).Count());
                if (sb.Length > 0)
                    sb.Remove(sb.Length - 1, 1);
                    
                log.Info("*** Connections UP: {0}, DOWN: {1} ({2}) ***", this.connections.Count, this.settings.ConnectionCount - this.connections.Count, sb);
            },
                null,
                30000,
                30000);

            this.recoveryTimer = new Timer(new TimerCallback(this.RecoveryCallback), null, 1000, 1000);
            this.cleanupTimer = new Timer(new TimerCallback(this.CleanupCallback), null, 5000, 5000);
            return;
        }

        private void Init()
        {
            this.connections = new List<NmsPooledConnection>();
            this.connectionsForCleanup = new List<NmsConnection>();
        }

        private void WireUpConnectionEvents(NmsConnection connection)
        {
            connection.ConnectionInterrupted += new EventHandler<NmsConnectionEventArgs>(connection_ConnectionInterrupted);
            connection.ConnectionResumed += new EventHandler<NmsConnectionEventArgs>(connection_ConnectionResumed);
            connection.ConnectionException += new EventHandler<NmsConnectionEventArgs>(connection_ConnectionException);
        }

        private void UnWireConnectionEvents(NmsConnection connection)
        {
            connection.ConnectionInterrupted -= new EventHandler<NmsConnectionEventArgs>(connection_ConnectionInterrupted);
            connection.ConnectionResumed -= new EventHandler<NmsConnectionEventArgs>(connection_ConnectionResumed);
            connection.ConnectionException -= new EventHandler<NmsConnectionEventArgs>(connection_ConnectionException);
        }
        
        /// <summary>
        /// Gets the next connection from the pool in round-robin fashion.
        /// </summary>
        /// <returns>The next connection from the pool.</returns>
        private NmsPooledConnection GetNextConnection()
        {
            lock (this.connections)
            {
                if (this.connections.Count == 0)
                    throw new InvalidOperationException("There are no active NMS connections available in the pool.");

                this.connectionIndex++;
                if (this.connectionIndex >= this.connections.Count)
                    this.connectionIndex = 0;

                return this.connections[this.connectionIndex];
            }
        }

        private void InnerStop()
        {
            log.Info("*** The connection pool is shutting down. ***");
            this.IsStarted = false;

            if (this.startThread != null)
            {
                this.startThread.Abort();
                this.startThread = null;
            }

            if (this.recoveryTimer != null)
            {
                this.recoveryTimer.Dispose();
                this.recoveryTimer = null;
            }
            if (this.statusTimer != null)
            {
                this.statusTimer.Dispose();
                this.statusTimer = null;
            }
            if (this.cleanupTimer != null)
            {
                this.cleanupTimer.Dispose();
                this.cleanupTimer = null;
            }

            foreach (var connection in this.connections)
            {
                this.UnWireConnectionEvents(connection);
                connection.Destroy();
            }
            this.connections.Clear();
        }

        private void AssertNotDisposed()
        {
            if (this.isDisposed)
                throw new ObjectDisposedException("ActiveMQConnectionPool");
        }

        private void AssertHasConnections()
        {
            lock (this.connections)
            {
                if (this.connections.Count == 0)
                throw new InvalidOperationException("Cannot perform this operation when there are no active connections available in the pool.");
            }
        }

        private void AssertIsStarted()
        {
                if (!this.IsStarted)
                    throw new InvalidOperationException("Cannot perform this operation when the pool is not started.");
        }

        private void VoidNewConnection()
        {
            this.NewConnection();
        }

        private NmsPooledConnection NewConnection()
        {
            this.pendingConnections++;
            NmsPooledConnection pooledConnection = null;

            try
            {
                log.Debug("Getting an endpoint from the endpoint manager.");
                var endPoint = this.endpointManager.GetNextNmsEndPoint();

                try
                {
                    log.Debug("Creating a new connection ({0}).", endPoint.Uri);
                    pooledConnection = endPoint.CreatePooledConnection(this.settings.AcknowledgementMode);

                    pooledConnection.connectionPool = this;
                    pooledConnection.settings = this.settings;
                    pooledConnection.endPoint = endPoint;

                    log.Debug("New connection ID is #{0}.", pooledConnection.id);

                    log.Debug("Wiring up the events.");
                    this.WireUpConnectionEvents(pooledConnection);

                    log.Debug("Starting the connection.");
                    pooledConnection.Start();

                    lock (this.connections)
                        this.connections.Add(pooledConnection);

                    this.startEvent.Set();

                    if (this.ConnectionResumed != null)
                        this.ConnectionResumed(this, new NmsConnectionEventArgs(pooledConnection));

                    if (this.ConnectionAvailable != null)
                        this.ConnectionAvailable(this, new NmsConnectionEventArgs(pooledConnection));

                    return pooledConnection;
                }
                catch (Exception ex)
                {
                    log.Error("Error creating connection: " + ex.Message);
                }
            }
            finally
            {
                this.pendingConnections--;
            }

            return null;
        }

        #endregion

        #region Static Methods [public]

        public static void StartGlobalInstance()
        {
            lock (instanceLock)
            {
                if (instance != null)
                    instance.Start();
                else
                {
                    instance = new NmsConnectionPool();
                    instance.Start();
                }
            }
        }

        public static void StopGlobalInstance()
        {
            lock (instanceLock)
            {
                instance.Stop();
                instance = null;
            }
        }

        #endregion

        #region Event Handlers

        void connection_ConnectionResumed(object sender, NmsConnectionEventArgs e)
        {
            var pooledConnection = sender as NmsPooledConnection;

            lock (this)
                this.connections.Add(pooledConnection);

            if (this.ConnectionResumed != null)
                this.ConnectionResumed(this, new NmsConnectionEventArgs(pooledConnection));
        }

        void connection_ConnectionInterrupted(object sender, NmsConnectionEventArgs e)
        {
            var pooledConnection = sender as NmsPooledConnection;

            lock (this.connections)
                this.connections.Remove(pooledConnection);
            lock (this.connectionsForCleanup)
                this.connectionsForCleanup.Add(pooledConnection);

            if (this.ConnectionInterrupted != null)
                this.ConnectionInterrupted(this, new NmsConnectionEventArgs(pooledConnection));

            
        }

        void connection_ConnectionException(object sender, NmsConnectionEventArgs e)
        {
            var pooledConnection = sender as NmsPooledConnection;

            lock (this.connections)
                this.connections.Remove(pooledConnection);
            lock (this.connectionsForCleanup)
                this.connectionsForCleanup.Add(pooledConnection);

            if (this.ConnectionInterrupted != null)
                this.ConnectionInterrupted(this, new NmsConnectionEventArgs(pooledConnection));
        }

        private void RecoveryCallback(object state)
        {
            if (this.settings == null)
                return;

            int down = this.settings.ConnectionCount - this.connections.Count - this.pendingConnections;
            //log.Debug("Pending: {0}", this.pendingConnections);

            if (isRecovering)
                return;

            isRecovering = true;

            try
            {
                if (down <= 0)
                    return;

                log.Info("Trying to recover {0} down connections.", down);
                for (int i = 0; i < down; i++)
                {
                    try
                    {
                        new Thread(new ThreadStart(this.VoidNewConnection)).Start();
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                    }
                }
            }
            finally
            {
                isRecovering = false;
            }
        }

        private void CleanupCallback(object state)
        {
            if (this.isCleaningUp)
                return;
            this.isCleaningUp = true;

            NmsConnection[] toCleanUp;
            lock (this.connectionsForCleanup)
                toCleanUp = this.connectionsForCleanup.ToArray();

            try
            {
                foreach (var connection in toCleanUp)
                {
                    try
                    {
                        log.Info("[{0}] Destroying connection.", connection.id);
                        connection.Dispose();
                        lock (this.connectionsForCleanup)
                            this.connectionsForCleanup.Remove(connection);
                    }
                    catch (Exception ex)
                    {
                        log.Warn("[{0}] Error when attempting to destroy connection: {1}", connection.id, ex.Message);
                    }
                }
            }
            finally
            {
                this.isCleaningUp = false;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            lock (this)
            {
                this.isDisposed = true;
                this.InnerStop();
                this.connectionFactory = null;
                this.connections = null;
                this.settings = null;
            }
        }

        #endregion

        #region INmsConnection Members

        int INmsConnection.ID
        {
            get { return 0; }
        }

        NmsProducer INmsConnection.CreateProducer(Destination destination)
        {
            return new NmsProducer(this, destination);
        }

        NmsProducer INmsConnection.CreateProducer(Destination destination, MsgDeliveryMode messageDeliveryMode)
        {
            return new NmsProducer(this, destination, messageDeliveryMode);
        }

        NmsProducer INmsConnection.CreateSynchronousProducer(Destination destination)
        {
            return this.GetConnection().CreateProducer(destination, synchronous: true);
        }

        NmsProducer INmsConnection.CreateSynchronousProducer(Destination destination, MsgDeliveryMode messageDeliveryMode)
        {
            return this.GetConnection().CreateProducer(destination, messageDeliveryMode: messageDeliveryMode, synchronous: true);
        }

        INmsSession INmsConnection.GetSession()
        {
            return this.GetConnection().GetSession();
        }

        INmsSession INmsConnection.GetSession(AcknowledgementMode acknowledgementMode)
        {
            return this.GetConnection().GetSession(acknowledgementMode);
        }

        NmsConsumer INmsConnection.CreateConsumer(Destination destination, Action<IMessage> messageReceivedCallback)
        {
            return new NmsConsumer(this, destination, messageReceivedCallback);
        }

        NmsConsumer INmsConnection.CreateConsumer(Destination destination, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback)
        {
            return new NmsConsumer(this, destination, messageReceivedCallback);
        }

        NmsMultiConsumer INmsConnection.CreateMultiConsumer(Destination destination, int consumerCount, Action<IMessage> messageReceivedCallback)
        {
            return new NmsMultiConsumer(this, destination, consumerCount, messageReceivedCallback);
        }

        NmsMultiConsumer INmsConnection.CreateMultiConsumer(Destination destination, int consumerCount, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback)
        {
            return new NmsMultiConsumer(this, destination, consumerCount, messageReceivedCallback);
        }

        #endregion

        private class RecoveryHelper
        {
            public int pendingConnections;
        }
    }
}
