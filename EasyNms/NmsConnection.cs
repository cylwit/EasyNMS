using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    public class NmsConnection : IDisposable, INmsConnection
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private static volatile int idCounter;

        public event EventHandler<NmsConnectionEventArgs> ConnectionInterrupted;
        public event EventHandler<NmsConnectionEventArgs> ConnectionResumed;
        public event EventHandler<NmsConnectionEventArgs> ConnectionException;

        #region Fields

        internal IConnection connection;
        internal int id;
        internal AcknowledgementMode acknowledgementMode;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the underlying IConnection for this instance.
        /// </summary>
        public IConnection Connection
        {
            get { return this.connection; }
        }

        /// <summary>
        /// Gets whether or not this instance has been destroyed.
        /// </summary>
        public bool IsDestroyed { get; protected set; }

        public int ID
        {
            get { return this.id; }
        }

        #endregion

        #region Constructors

        internal NmsConnection(IConnection connection)
            : this(connection, Apache.NMS.AcknowledgementMode.AutoAcknowledge)
        {
        }

        /// <summary>
        /// Creates a new ActiveMQConnection instance with the specified underlying IConnection and optionally specifying the default acknowledgement
        /// mode for new sessions.
        /// </summary>
        /// <param name="connection">The IConnection instance which this ActiveMQConnection should wrap.</param>
        /// <param name="acknowledgementMode">Optionally specify the default acknowledgement mode for new sessions.</param>
        internal NmsConnection(IConnection connection, Apache.NMS.AcknowledgementMode acknowledgementMode)
        {
            this.connection = connection;
            this.id = idCounter++;
            this.acknowledgementMode = acknowledgementMode;
            this.WireUpEvents();
        }

        public NmsConnection(IConnectionFactory connectionFactory, Apache.NMS.AcknowledgementMode acknowledgementMode, NmsCredentials credentials)
        {
            this.connection = (credentials == null)
                ? connectionFactory.CreateConnection()
                : connectionFactory.CreateConnection(credentials.Username, credentials.Password);
            this.acknowledgementMode = acknowledgementMode;
            this.id = idCounter++;
            this.WireUpEvents();
        }

        public NmsConnection(Uri uri, Apache.NMS.AcknowledgementMode acknowledgementMode, NmsCredentials credentials, params object[] connectionFactoryConstructorParameters)
        {
            var factory = NMSConnectionFactory.CreateConnectionFactory(uri, connectionFactoryConstructorParameters);
            this.connection = (credentials == null)
                ? factory.CreateConnection()
                : factory.CreateConnection(credentials.Username, credentials.Password);
            this.id = idCounter++;
            this.WireUpEvents();
        }

        #endregion

        #region Methods [public] [virtual]

        /// <summary>
        /// Starts the underlying connection.
        /// </summary>
        public virtual void Start()
        {
            this.AssertNotDestroyed();
            this.connection.Start();
        }

        /// <summary>
        /// Stops the underlying connection.
        /// </summary>
        public virtual void Stop()
        {
            this.AssertNotDestroyed();
            this.connection.Stop();
        }

        /// <summary>
        /// Creates a new session using the default acknowledgement mode for this connection.
        /// </summary>
        /// <returns>A new NmsSession object wrapping a new ISession using the default acknowledgement mode for this connection.</returns>
        public virtual NmsSession CreateSession()
        {
            this.AssertNotDestroyed();

            var session = this.connection.CreateSession(this.acknowledgementMode);
            return new NmsSession(this.connection, session);
        }

        /// <summary>
        /// Create a new session, optionally specifying the acknowledgement mode.
        /// </summary>
        /// <param name="acknowledgementMode">The acknowledgement mode to use.  Default is auto-acknowledge.</param>
        /// <returns>A new NmsSession object wrapping a new ISession using the specified acknowledgement mode.</returns>
        public virtual NmsSession CreateSession(AcknowledgementMode acknowledgementMode)
        {
            this.AssertNotDestroyed();

            ISession newSession = this.connection.CreateSession(acknowledgementMode);
            return new NmsSession(this.connection, newSession);
        }

        #endregion
        #region Methods [public]

        /// <summary>
        /// Fluent setter for the default acknowledgement mode for this connection.
        /// </summary>
        /// <param name="mode">The default acknowledgement mode to use for this connection.</param>
        /// <returns></returns>
        public NmsConnection AcknowledgementMode(AcknowledgementMode mode)
        {
            this.acknowledgementMode = mode;
            return this;
        }

        /// <summary>
        /// Creates a new multi-consumer for this connection which will process messages without returning a response.
        /// </summary>
        /// <param name="destination">The destination queue to consume from.</param>
        /// <param name="consumerCount">The number of consumers to listen to the destination queue with.</param>
        /// <param name="messageReceivedCallback">The callback to use when a message is received from the destination queue.</param>
        /// <param name="selector">Optionally provide a message selector.</param>
        /// <returns>A new, initialized ActiveMQMultiConsumer instance.</returns>
        public NmsMultiConsumer CreateMultiConsumer(Destination destination, int consumerCount, Action<IMessage> messageReceivedCallback, string selector = null)
        {
            return new NmsMultiConsumer(this, destination, consumerCount, messageReceivedCallback, selector);
        }

        /// <summary>
        /// Creates a new multi-consumer for this connection which will process messages and return a response.
        /// </summary>
        /// <param name="destination">The destination queue to consume from.</param>
        /// <param name="consumerCount">The number of consumers to listen to the destination queue with.</param>
        /// <param name="messageReceivedCallback">The callback to use when a message is received from the destination queue.</param>
        /// <param name="selector">Optionally provide a message selector.</param>
        /// <returns>A new, initialized ActiveMQMultiConsumer instance.</returns>
        [Obsolete]
        public NmsMultiConsumer CreateMultiConsumer(Destination destination, int consumerCount, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
        {
            return new NmsMultiConsumer(this, destination, consumerCount, messageReceivedCallback, selector);
        }

        [Obsolete]
        public NmsConsumer CreateConsumer(Destination destination, Action<IMessage> messageReceivedCallback, string selector = null)
        {
            return new NmsConsumer(this, destination, messageReceivedCallback, selector);
        }

        [Obsolete]
        public NmsConsumer CreateConsumer(Destination destination, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
        {
            return new NmsConsumer(this, destination, messageReceivedCallback, selector);
        }

        public NmsProducer CreateProducer(Destination destination, MsgDeliveryMode messageDeliveryMode = MsgDeliveryMode.Persistent, bool synchronous = false)
        {
            return new NmsProducer(this, destination, messageDeliveryMode, synchronous);
        }

        #endregion
        #region Methods [internal] [virtual]

        /// <summary>
        /// Destroys this connection, disposing of all underlying and instance resources.
        /// </summary>
        internal virtual void Destroy()
        {
            this.UnwireEvents();
            if (this.connection.IsStarted || !this.IsDestroyed)
            {
                this.connection.Stop();
                this.connection.Dispose();
            }
            this.connection = null;
            this.IsDestroyed = true;
        }

        #endregion
        #region Methods [private]

        private void WireUpEvents()
        {
            this.connection.ExceptionListener += new ExceptionListener(connection_ExceptionListener);
            this.connection.ConnectionInterruptedListener += new ConnectionInterruptedListener(connection_ConnectionInterruptedListener);
            this.connection.ConnectionResumedListener += new ConnectionResumedListener(connection_ConnectionResumedListener);
        }

        private void UnwireEvents()
        {
            this.connection.ExceptionListener -= new ExceptionListener(connection_ExceptionListener);
            this.connection.ConnectionInterruptedListener -= new ConnectionInterruptedListener(connection_ConnectionInterruptedListener);
            this.connection.ConnectionResumedListener -= new ConnectionResumedListener(connection_ConnectionResumedListener);
        }

        /// <summary>
        /// Asserts that this instance has not been destroyed/disposed.
        /// </summary>
        private void AssertNotDestroyed()
        {
            lock (this)
            {
                if (this.IsDestroyed)
                    throw new InvalidOperationException("Cannot access ActiveMQConnection once it has been destroyed.");
            }
        }

        #endregion

        #region Event Handlers

        void connection_ExceptionListener(Exception exception)
        {
            log.Warn("[{0}] An exception occurred on the connection level: {1}", this.id, exception.Message);
            //log.Warn(exception);
            if (this.ConnectionException != null)
                this.ConnectionException(this, new NmsConnectionEventArgs(this));
        }

        void connection_ConnectionResumedListener()
        {
            log.Info("[{0}] Connection resumed.", this.id);
            if (this.ConnectionResumed != null)
                this.ConnectionResumed(this, new NmsConnectionEventArgs(this));
        }

        void connection_ConnectionInterruptedListener()
        {
            log.Warn("[{0}] Connection interrupted!", this.id);
            if (this.ConnectionInterrupted != null)
                this.ConnectionInterrupted(this, new NmsConnectionEventArgs(this));
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Destroys this instance, disposing of all underlying and instance resources.
        /// </summary>
        public void Dispose()
        {
            this.AssertNotDestroyed();
            this.Destroy();
        }

        #endregion

        #region INmsConnection Members

        public virtual INmsSession GetSession()
        {
            this.AssertNotDestroyed();

            var session = this.connection.CreateSession(this.acknowledgementMode);
            return new NmsSession(this.connection, session);
        }

        public INmsSession GetSession(AcknowledgementMode acknowledgementMode)
        {
            this.AssertNotDestroyed();

            ISession newSession = this.connection.CreateSession(acknowledgementMode);
            return new NmsSession(this.connection, newSession);
        }

        public NmsProducer CreateProducer(Destination destination)
        {
            return new NmsProducer(this, destination);
        }

        public NmsProducer CreateProducer(Destination destination, MsgDeliveryMode messageDeliveryMode)
        {
            return new NmsProducer(this, destination, deliveryMode: messageDeliveryMode);
        }

        public NmsProducer CreateSynchronousProducer(Destination destination)
        {
            return new NmsProducer(this, destination, synchronous: true);
        }

        public NmsProducer CreateSynchronousProducer(Destination destination, MsgDeliveryMode messageDeliveryMode)
        {
            return new NmsProducer(this, destination, deliveryMode: messageDeliveryMode, synchronous: true);
        }

        public NmsConsumer CreateConsumer(Destination destination, Action<IMessage> messageReceivedCallback)
        {
            return new NmsConsumer(this, destination, messageReceivedCallback);
        }

        public NmsConsumer CreateConsumer(Destination destination, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback)
        {
            return new NmsConsumer(this, destination, messageReceivedCallback);
        }

        public NmsMultiConsumer CreateMultiConsumer(Destination destination, int consumerCount, Action<IMessage> messageReceivedCallback)
        {
            return new NmsMultiConsumer(this, destination, consumerCount, messageReceivedCallback);
        }

        public NmsMultiConsumer CreateMultiConsumer(Destination destination, int consumerCount, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback)
        {
            return new NmsMultiConsumer(this, destination, consumerCount, messageReceivedCallback);
        }

        #endregion
    }
}
