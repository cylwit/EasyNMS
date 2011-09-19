using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Apache.NMS;
using EasyNms.Connections;
using EasyNms.Sessions;

namespace EasyNms
{
    public class NmsConsumer : IDisposable
    {
        #region Fields

        private NmsConnection connection;
        private NmsSession session;
        private IMessageConsumer consumer;
        private IMessageProducer replyProducer;
        private Func<MessageFactory, IMessage, IMessage> requestReplyCallback;
        private Action<IMessage> requestOnlyCallback;
        private bool isDisposed;
        private bool isInitialized;

        #endregion

        #region Constructors

        private NmsConsumer(NmsConnection connection)
        {
            this.connection = connection;
            this.session = connection.CreateSession();
        }

        internal NmsConsumer(NmsConnection connection, Destination destination, Action<IMessage> messageReceivedCallback, string selector = null)
            : this(connection)
        {
            this.consumer = (selector == null)
                ? this.session.Session.CreateConsumer(destination.GetDestination(this.session))
                : session.Session.CreateConsumer(destination.GetDestination(this.session), selector);

            this.requestOnlyCallback = messageReceivedCallback;
            this.consumer.Listener += new MessageListener(this.RequestOnlyCallback);

            this.isInitialized = true;
        }

        internal NmsConsumer(NmsConnection connection, Destination destination, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
            : this(connection)
        {
            this.requestReplyCallback = messageReceivedCallback;

            this.consumer = (selector == null)
                ? this.session.Session.CreateConsumer(destination.GetDestination(this.session))
                : session.Session.CreateConsumer(destination.GetDestination(this.session), selector);

            this.consumer.Listener += new MessageListener(this.RequestReplyCallback);

            this.replyProducer = this.session.Session.CreateProducer();
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
            this.requestOnlyCallback(message);
        }

        /// <summary>
        /// Called when a message is received in request-response.
        /// </summary>
        /// <param name="message">The received message.</param>
        void RequestReplyCallback(IMessage message)
        {
            // Wait on this event until everything is setup (in-case we get a message before the delegate for this method is wired up, however unlikely).
            while (!this.isInitialized)
                Thread.Sleep(1);

            // Assert that we haven't disposed of this object.
            this.AssertIsNotDisposed();

            // Process the message with the request-reply callback.
            var replyMessage = this.requestReplyCallback(this.session.messageFactory, message);

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
            catch (Exception)
            {
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

                this.replyProducer.Dispose();
                this.replyProducer = null;

                this.requestReplyCallback = null;

                this.session.Dispose();
                this.session = null;

                this.connection = null;
            }
        }

        #endregion
    }
}
