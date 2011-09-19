using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using EasyNms.Sessions;
using System.Diagnostics;

namespace EasyNms.Connections
{
    public class NmsConnection : IDisposable
    {
        #region Fields

        private SessionFactory sessionFactory;
        internal IConnection connection;

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

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new ActiveMQConnection instance with the specified underlying IConnection and optionally specifying the default acknowledgement
        /// mode for new sessions.
        /// </summary>
        /// <param name="connection">The IConnection instance which this ActiveMQConnection should wrap.</param>
        /// <param name="acknowledgementMode">Optionally specify the default acknowledgement mode for new sessions.</param>
        internal NmsConnection(IConnection connection)
        {
            this.connection = connection;
        }

        public NmsConnection(IConnectionFactory connectionFactory, NmsCredentials credentials = null)
        {
            this.connection = (credentials == null)
                ? connectionFactory.CreateConnection()
                : connectionFactory.CreateConnection(credentials.Username, credentials.Password);
            this.connection.ExceptionListener += new ExceptionListener(connection_ExceptionListener);
        }

        public NmsConnection(Uri uri, NmsCredentials credentials = null, params object[] connectionFactoryConstructorParameters)
        {
            var factory = NMSConnectionFactory.CreateConnectionFactory(uri, connectionFactoryConstructorParameters);
            this.connection = (credentials == null)
                ? factory.CreateConnection()
                : factory.CreateConnection(credentials.Username, credentials.Password);
            this.connection.ExceptionListener += new ExceptionListener(connection_ExceptionListener);
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
            this.Destroy();
        }

        #endregion

        #region Event Handlers

        void connection_ExceptionListener(Exception exception)
        {
            Debug.WriteLine(exception);
        }

        #endregion
    }
}
