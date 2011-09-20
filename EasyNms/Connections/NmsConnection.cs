using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using EasyNms.Sessions;
using System.Diagnostics;

namespace EasyNms.Connections
{
    public class NmsConnection :IConnection, IDisposable
    {
        #region Events

        public event EventHandler<ConnectionExceptionEventArgs> ConnectionException;
        public event EventHandler<EventArgs> ConnectionInterrupted;
        public event EventHandler<EventArgs> ConnectionResumed;

        #endregion

        #region Fields

        private SessionFactory sessionFactory;
        internal IConnection connection;

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether or not this instance has been destroyed.
        /// </summary>
        public bool IsDestroyed { get; protected set; }

        /// <summary>
        /// Gets or sets the client ID for this connection.
        /// </summary>
        public string ClientID
        {
            get { return this.connection.ClientId; }
            set { this.connection.ClientId = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new NmsConnection instance with the specified underlying IConnection.
        /// </summary>
        /// <param name="connection">The IConnection instance which this NmsConnection should wrap.</param>
        internal NmsConnection(IConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");

            this.connection = connection;
            this.AttachConnectionEventHandlers();
        }

        /// <summary>
        /// Creates a new NmsConnection using the specified IConnectionFactory, optionally providing credentials.
        /// </summary>
        /// <param name="connectionFactory">The IConnectionFactory to use to create the connection.</param>
        /// <param name="credentials">The connection credentials to use to connect to the NMS broker.</param>
        public NmsConnection(IConnectionFactory connectionFactory, INmsCredentials credentials = null)
        {
            if (connectionFactory == null)
                throw new ArgumentNullException("connectionFactory");

            this.CreateConnection(connectionFactory, credentials);
        }

        /// <summary>
        /// Creates a new NmsConnection using the specified URI, optionally providing credentials.  This constructor uses Apache.NMS.NMSConnectionFactory.CreateConnectionFactory()
        /// to resolve the appropriate connection factory.
        /// </summary>
        /// <param name="uri">The NMS URI of the broker.</param>
        /// <param name="credentials">The connection credentials to use to connect to the NMS broker.</param>
        /// <param name="connectionFactoryConstructorParameters">Any additional arguments which need to be passed to the connection factory constructor.</param>
        public NmsConnection(Uri uri, INmsCredentials credentials = null, params object[] connectionFactoryConstructorParameters)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            var factory = NMSConnectionFactory.CreateConnectionFactory(uri, connectionFactoryConstructorParameters);
            this.CreateConnection(factory, credentials);
        }

        #endregion

        #region Methods [public] [virtual]

        public virtual NmsConnection SessionFactory(SessionFactory sessionFactory)
        {
            if (sessionFactory == null)
                throw new ArgumentNullException("sessionFactory");

            this.AssertNotDestroyed();
            this.sessionFactory = sessionFactory;
            this.sessionFactory.SetConnection(this.connection);
            return this;
        }

        /// <summary>
        /// Starts the underlying connection.
        /// </summary>
        public virtual NmsConnection Start()
        {
            this.AssertNotDestroyed();
            this.connection.Start();
            if (this.sessionFactory == null)
                this.sessionFactory = new StandardSessionFactory();
            this.sessionFactory.SetConnection(this.connection);
            this.sessionFactory.Start();
            return this;
        }

        /// <summary>
        /// Stops the underlying connection.
        /// </summary>
        public virtual void Stop()
        {
            this.AssertNotDestroyed();
            this.sessionFactory.Stop();
            this.connection.Stop();
        }

        /// <summary>
        /// Create a new session.
        /// </summary>
        /// <returns></returns>
        public virtual NmsSession CreateSession()
        {
            this.AssertNotDestroyed();
            return this.sessionFactory.CreateSession();
        }

        public virtual NmsSession CreateSession(AcknowledgementMode acknowledgementMode)
        {
            this.AssertNotDestroyed();
            return this.sessionFactory.CreateSession(acknowledgementMode);
        }

        #endregion
        #region Methods [public]
        
        /// <summary>
        /// Creates a new multi-consumer for this connection which will process messages without returning a response.
        /// </summary>
        /// <param name="destination">The destination queue to consume from.</param>
        /// <param name="consumerCount">The number of consumers to listen to the destination queue with.</param>
        /// <param name="messageReceivedCallback">The callback to use when a message is received from the destination queue.</param>
        /// <param name="selector">Optionally provide a message selector.</param>
        /// <returns>A new, initialized ActiveMQMultiConsumer instance.</returns>
        public NmsPooledConsumer CreatePooledConsumer(Destination destination, int consumerCount, Action<IMessage> messageReceivedCallback, string selector = null)
        {
            return new NmsPooledConsumer(this, destination, consumerCount, messageReceivedCallback, selector);
        }

        /// <summary>
        /// Creates a new multi-consumer for this connection which will process messages and return a response.
        /// </summary>
        /// <param name="destination">The destination queue to consume from.</param>
        /// <param name="consumerCount">The number of consumers to listen to the destination queue with.</param>
        /// <param name="messageReceivedCallback">The callback to use when a message is received from the destination queue.</param>
        /// <param name="selector">Optionally provide a message selector.</param>
        /// <returns>A new, initialized ActiveMQMultiConsumer instance.</returns>
        public NmsPooledConsumer CreatePooledConsumer(Destination destination, int consumerCount, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
        {
            return new NmsPooledConsumer(this, destination, consumerCount, messageReceivedCallback, selector);
        }

        public NmsProducer CreateProducer(Destination destination, MsgDeliveryMode messageDeliveryMode = MsgDeliveryMode.Persistent, bool synchronous = false)
        {
            return new NmsProducer(this, destination, messageDeliveryMode, synchronous);
        }

        public NmsConsumer CreateConsumer(Destination destination, Action<IMessage> messageReceivedCallback, string selector = null)
        {
            return new NmsConsumer(this, destination, messageReceivedCallback, selector);
        }

        public NmsConsumer CreateConsumer(Destination destination, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
        {
            return new NmsConsumer(this, destination, messageReceivedCallback, selector);
        }

        #endregion
        #region Methods [internal] [virtual]

        /// <summary>
        /// Destroys this connection, disposing of all underlying and instance resources.
        /// </summary>
        internal virtual void Destroy()
        {
            this.connection.Dispose();
            this.connection = null;
            this.IsDestroyed = true;
        }

        #endregion
        #region Methods [private]

        /// <summary>
        /// Creates a new connection using the specified connection factory and credentials. 
        /// </summary>
        /// <param name="factory">The connection factory to use to create the connection.</param>
        /// <param name="credentials">The credentials to use to connect to the NMS broker.</param>
        private void CreateConnection(IConnectionFactory factory, INmsCredentials credentials)
        {
            this.connection = (credentials == null)
                ? factory.CreateConnection()
                : factory.CreateConnection(credentials.Username, credentials.Password);

            this.AttachConnectionEventHandlers();
        }

        /// <summary>
        /// Attaches event handlers to IConnection's events.
        /// </summary>
        private void AttachConnectionEventHandlers()
        {
            this.connection.ExceptionListener += new ExceptionListener(connection_ExceptionListener);
            this.connection.ConnectionInterruptedListener += new ConnectionInterruptedListener(connection_ConnectionInterruptedListener);
            this.connection.ConnectionResumedListener += new ConnectionResumedListener(connection_ConnectionResumedListener);
        }

        /// <summary>
        /// Detaches event handlers from IConnection's events.
        /// </summary>
        private void DetachConnectionEventHandlers()
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

        #region IDisposable Members

        /// <summary>
        /// Destroys this instance, disposing of all underlying and instance resources.
        /// </summary>
        public void Dispose()
        {
            this.AssertNotDestroyed();
            this.DetachConnectionEventHandlers();
            this.Destroy();
        }

        #endregion

        #region Event Handlers

        // I feel that it's important to receive the sender in this type of events in-case you're managing multiple connections
        // with the same event handler; that is why the events are wrapped here and the IConnection events are only exposed via
        // the interface.

        void connection_ExceptionListener(Exception exception)
        {
            if (this.exceptionListener != null)
                this.exceptionListener(exception);
            if (this.ConnectionException != null)
                this.ConnectionException(this, new ConnectionExceptionEventArgs(exception));
        }

        void connection_ConnectionResumedListener()
        {
            if (this.connectionResumedListener != null)
                this.connectionResumedListener();
            if (this.ConnectionResumed != null)
                this.ConnectionResumed(this, new EventArgs());
        }

        void connection_ConnectionInterruptedListener()
        {
            if (this.connectionInterruptedListener != null)
                this.connectionInterruptedListener();
            if (this.ConnectionInterrupted != null)
                this.ConnectionInterrupted(this, new EventArgs());
        }

        #endregion

        #region IConnection Members

        private ConnectionInterruptedListener connectionInterruptedListener;
        private ConnectionResumedListener connectionResumedListener;
        private ExceptionListener exceptionListener;

        event ConnectionInterruptedListener IConnection.ConnectionInterruptedListener
        {
            add { connectionInterruptedListener += value; }
            remove { connectionInterruptedListener -= value; }
        }

        event ConnectionResumedListener IConnection.ConnectionResumedListener
        {
            add { connectionResumedListener += value; }
            remove { connectionResumedListener -= value; }
        }

        event ExceptionListener IConnection.ExceptionListener
        {
            add { exceptionListener += value; }
            remove { exceptionListener -= value; }
        }

        public AcknowledgementMode AcknowledgementMode
        {
            get { return this.connection.AcknowledgementMode; }
            set { this.connection.AcknowledgementMode = value; }
        }

        string IConnection.ClientId
        {
            get { return this.connection.ClientId; }
            set { this.connection.ClientId = value; }
        }

        public ConsumerTransformerDelegate ConsumerTransformer
        {
            get { return this.connection.ConsumerTransformer; }
            set { this.connection.ConsumerTransformer = value; }
        }

        public IConnectionMetaData MetaData
        {
            get { return this.connection.MetaData; }
        }

        public ProducerTransformerDelegate ProducerTransformer
        {
            get { return this.connection.ProducerTransformer; }
            set { this.connection.ProducerTransformer = value; }
        }

        public IRedeliveryPolicy RedeliveryPolicy
        {
            get { return this.connection.RedeliveryPolicy; }
            set { this.connection.RedeliveryPolicy = value; }
        }

        public TimeSpan RequestTimeout
        {
            get { return this.connection.RequestTimeout; }
            set { this.connection.RequestTimeout = value; }
        }

        public bool IsStarted
        {
            get { return this.connection.IsStarted; }
        }

        void IConnection.Close()
        {
            this.Stop();
        }

        ISession IConnection.CreateSession(AcknowledgementMode acknowledgementMode)
        {
            return this.sessionFactory.CreateSession(acknowledgementMode).Session;
        }

        ISession IConnection.CreateSession()
        {
            return this.sessionFactory.CreateSession().Session;
        }

        void IStartable.Start()
        {
            this.Start();
        }

        #endregion
    }
}
