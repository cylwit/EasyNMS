using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using System.Threading;

namespace EasyNms
{
    public class NmsConsumer : IDisposable
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private static volatile int idCounter;

        #region Fields

        private INmsConnection connection;
        private INmsSession session;
        private IMessageConsumer consumer;
        private IMessageProducer replyProducer;
        private Func<MessageFactory, IMessage, IMessage> requestReplyCallback;
        private Action<IMessage> requestOnlyCallback;
        private bool isDisposed;
        private bool isInitialized;
        private Destination destination;
        private string selector;
        private int id;

        #endregion

        #region Constructors

        private NmsConsumer(INmsConnection connection, Destination destination, string selector)
        {
            this.id = idCounter++;
            this.selector = selector;
            this.destination = destination;
            this.connection = connection;
            this.connection.ConnectionInterrupted += new EventHandler<NmsConnectionEventArgs>(connection_ConnectionInterrupted);
            this.connection.ConnectionResumed += new EventHandler<NmsConnectionEventArgs>(connection_ConnectionResumed);
        }

        internal NmsConsumer(INmsConnection connection, Destination destination, Action<IMessage> messageReceivedCallback, string selector = null)
            : this(connection, destination, selector)
        {
            this.requestOnlyCallback = messageReceivedCallback;
            this.SetupRequestOnly(connection, destination, messageReceivedCallback, selector);
        }

        internal NmsConsumer(INmsConnection connection, Destination destination, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
            : this(connection, destination, selector)
        {
            
            this.requestReplyCallback = messageReceivedCallback;
            this.SetupRequestReply(connection, destination, messageReceivedCallback, selector);
        }

        #endregion

        #region Methods [internal]

        private void SetupRequestOnly(INmsConnection connection, Destination destination, Action<IMessage> messageReceivedCallback, string selector = null)
        {
            this.session = connection.GetSession();
            this.consumer = (selector == null)
                ? this.session.CreateConsumer(destination.GetDestination(this.session))
                : session.CreateConsumer(destination.GetDestination(this.session), selector);
            

            this.consumer.Listener += new MessageListener(this.RequestOnlyCallback);

            this.isInitialized = true;
        }

        private void SetupRequestReply(INmsConnection connection, Destination destination, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
        {
            this.session = connection.GetSession();

            this.consumer = (selector == null)
                ? this.session.CreateConsumer(destination.GetDestination(this.session))
                : session.CreateConsumer(destination.GetDestination(this.session), selector);

            this.consumer.Listener += new MessageListener(this.RequestReplyCallback);

            this.replyProducer = this.session.CreateProducer();
            this.replyProducer.DeliveryMode = MsgDeliveryMode.NonPersistent;

            this.isInitialized = true;
        }

        #endregion
        #region Methods [private]

        /// <summary>
        /// Asserts that this instance has not been disposed.
        /// </summary>
        private void AssertIsNotDisposed()
        {
            lock (this)
            {
                if (this.isDisposed)
                    throw new ObjectDisposedException("ActiveMQConsumer");
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Called when a message is received in request-only mode.
        /// </summary>
        /// <param name="message"></param>
        void RequestOnlyCallback(IMessage message)
        {
            log.Debug("Received request-only message.");
            log.Trace(message);

            try
            {
                this.requestOnlyCallback(message);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Called when a message is received in request-response.
        /// </summary>
        /// <param name="message">The received message.</param>
        void RequestReplyCallback(IMessage message)
        {
            log.Debug("Received request-reply message.");
            log.Trace(message);

            try
            {

                // Wait on this event until everything is setup (in-case we get a message before the delegate for this method is wired up, however unlikely).
                while (!this.isInitialized)
                    Thread.Sleep(1);

                // Assert that we haven't disposed of this object.
                this.AssertIsNotDisposed();

                // Process the message with the request-reply callback.
                var replyMessage = this.requestReplyCallback(this.session.MessageFactory, message);

                // If no reply-to destination was specified, we don't need to bother sending a response.
                if (message.NMSReplyTo == null)
                    return;

                // Set the correlation ID to the received message's correlation ID.
                replyMessage.NMSCorrelationID = message.NMSCorrelationID;

                try
                {
                    // Send the response to the destination specified in the reply-to.
                    this.replyProducer.Send(message.NMSReplyTo, replyMessage);
                }
                catch (Exception ex)
                {
                    log.Warn("Failed to send response: {0}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Occurs when the underlying connection has been interrupted.
        /// </summary>
        void connection_ConnectionInterrupted(object sender, NmsConnectionEventArgs e)
        {
            lock (this)
            {
                if (this.connection != sender)
                    return;

                log.Warn("[{0}] Consumer #{1}'s connection was lost.  Attempting to recreate.", e.Connection.ID, this.id);

                if (this.requestOnlyCallback != null)
                    this.consumer.Listener -= new MessageListener(this.RequestOnlyCallback);
                else if (this.requestReplyCallback != null)
                    this.consumer.Listener -= new MessageListener(this.RequestReplyCallback);

                this.consumer.Dispose();
                this.consumer = null;
                this.isInitialized = false;

                try
                {
                    if (this.requestOnlyCallback != null)
                        this.SetupRequestOnly(this.connection, this.destination, this.requestOnlyCallback, this.selector);
                    else if (this.requestReplyCallback != null)
                        this.SetupRequestReply(this.connection, this.destination, this.requestReplyCallback, this.selector);
                }
                catch (Exception ex)
                {
                    log.Warn("[{0}] Failed to recreate consumer using this connection ({1}).  Waiting for resume condition to restart.", e.Connection.ID, ex.Message);
                }
            }
        }

        void connection_ConnectionResumed(object sender, NmsConnectionEventArgs e)
        {
            lock (this)
            {
                if (this.isInitialized)
                    return;

                log.Info("[{0}] Resuming consumer #{1} on this connection.", e.Connection.ID, this.id);

                this.connection = (INmsConnection)sender;

                if (this.requestOnlyCallback != null)
                    this.SetupRequestOnly(this.connection, this.destination, this.requestOnlyCallback, this.selector);
                else if (this.requestReplyCallback != null)
                    this.SetupRequestReply(this.connection, this.destination, this.requestReplyCallback, this.selector);
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            lock (this)
            {
                this.isDisposed = true;
                this.consumer.Dispose();
                this.consumer = null;

                if (this.replyProducer != null)
                {
                    this.replyProducer.Dispose();
                    this.replyProducer = null;
                }

                this.requestReplyCallback = null;

                this.session.Dispose();
                this.session = null;

                this.connection.ConnectionInterrupted -= new EventHandler<NmsConnectionEventArgs>(connection_ConnectionInterrupted);
                this.connection.ConnectionResumed -= new EventHandler<NmsConnectionEventArgs>(connection_ConnectionResumed);

                this.connection = null;
            }
        }

        #endregion
    }
}
