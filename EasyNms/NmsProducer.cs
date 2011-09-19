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
    public class NmsProducer : IDisposable
    {
        #region Fields

        private IMessageProducer producer;
        private Dictionary<string, AsyncMessageHelper> responseBuffer;
        private NmsSession session;
        private NmsConnection connection;
        private ITemporaryQueue temporaryQueue;
        private IMessageConsumer responseConsumer;
        private bool isInitializedForSynchronous;
        private IDestination destination;
        private MessageFactory messageFactory;

        #endregion

        #region Properties

        public MsgDeliveryMode DeliveryMode
        {
            get { return this.producer.DeliveryMode; }
            set { this.producer.DeliveryMode = value; }
        }

        public MessageFactory @MessageFactory
        {
            get { return this.messageFactory; }
        }

        #endregion

        #region Constructors

        private NmsProducer(NmsConnection connection)
        {
            this.connection = connection;
            this.session = connection.CreateSession();
            this.messageFactory = new MessageFactory(this.session);
        }

        internal NmsProducer(NmsConnection connection, Destination destination, MsgDeliveryMode deliveryMode = MsgDeliveryMode.Persistent, bool synchronous = false)
            : this(connection)
        {
            this.destination = destination.GetDestination(this.session);
            this.producer = this.session.Session.CreateProducer(this.destination);
            this.producer.DeliveryMode = deliveryMode;

            if (synchronous)
                this.InitializeForSynchronous();
        }

        #endregion

        #region Methods [public]

        public void SendRequest(IMessage message)
        {
            this.producer.Send(message);
        }

        public void SendRequest(Destination destination, IMessage message)
        {
            this.producer.Send(destination.GetDestination(this.session), message);
        }

        public void SendRequest(IMessage message, MsgDeliveryMode deliveryMode, MsgPriority messagePriority, TimeSpan timeToLive)
        {
            this.producer.Send(message, deliveryMode, messagePriority, timeToLive);
        }

        public void SendRequest(Destination destination, IMessage message, MsgDeliveryMode deliveryMode, MsgPriority messagePriority, TimeSpan timeToLive)
        {
            this.producer.Send(destination.GetDestination(this.session), message, deliveryMode, messagePriority, timeToLive);
        }

        public IMessage SendRequestResponse(IMessage message, int timeoutInMilliseconds = 15000)
        {
            this.InitializeForSynchronous();

            // Create a unique correlation ID which we will use to map response messages to request messages.
            var correlationID = Guid.NewGuid().ToString();
            message.NMSCorrelationID = correlationID;

            // Set the reply-to header to the temporary queue that we created so that the server knows where to return messages.
            message.NMSReplyTo = this.temporaryQueue;

            // Create a new AsyncMessageHelper.  This class is a helper class to make it easier for us to map response messages to request messages.
            using (var asyncMessageHelper = new AsyncMessageHelper())
            {
                // Add the async helper to the response buffer.
                lock (this.responseBuffer)
                    this.responseBuffer[correlationID] = asyncMessageHelper;

                // Send the message to the queue.
                this.producer.Send(message);

                // Wait for a response for up to [timeout] seconds.  This blocks until the timeout expires or a message is received (.Set() is called on the trigger then, allowing execution to continue).
                asyncMessageHelper.Trigger.WaitOne(timeoutInMilliseconds, true);

                // Either the timeout has expired, or a message was received with the same correlation ID as the request message.
                IMessage responseMessage;
                try
                {
                    // The Message property on the async helper will not have been set if no message was received within the timeout period.
                    if (asyncMessageHelper.Message == null)
                        throw new TimeoutException("Timed out while waiting for a response.");

                    // We got the response message, cool!
                    responseMessage = asyncMessageHelper.Message;
                }
                finally
                {
                    // Remove the async helper from the response buffer.
                    lock (this.responseBuffer)
                        this.responseBuffer.Remove(correlationID);
                }

                // Return the response message.
                return responseMessage;
            }
        }

        #endregion
        #region Methods [private]

        /// <summary>
        /// Sets up the response buffer, temporary queue, response consumer and listener delegates for sending and receiving synchronous messages.
        /// </summary>
        private void InitializeForSynchronous()
        {
            lock (this)
            {
                if (this.isInitializedForSynchronous)
                    return;

                this.responseBuffer = new Dictionary<string, AsyncMessageHelper>();
                this.temporaryQueue = this.session.CreateTemporaryQueue(useCachedQueue: true);
                this.responseConsumer = this.session.Session.CreateConsumer(this.temporaryQueue);
                this.responseConsumer.Listener += new MessageListener(responseConsumer_Listener);
                this.isInitializedForSynchronous = true;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// This event will fire when a response message is received from the temporary queue.  It will attempt to map received messages back
        /// to the response buffer.
        /// </summary>
        void responseConsumer_Listener(IMessage message)
        {
            // Look for an async helper with the same correlation ID.
            AsyncMessageHelper asyncMessageHelper;
            lock (this.responseBuffer)
            {
                // If no async helper with the same correlation ID exists, then we've received some erranious message that we don't care about.
                if (!this.responseBuffer.TryGetValue(message.NMSCorrelationID, out asyncMessageHelper))
                    return;
            }

            // Set the Message property so we can access it in the send method.
            asyncMessageHelper.Message = message;

            // Fire the trigger so that the send method stops blocking and continues on its way.
            asyncMessageHelper.Trigger.Set();
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Helper class which assists with keeping track of message timeouts, blocking the sending thread and carrying messages through the async eventing back
        /// to the synchronous call.
        /// </summary>
        private class AsyncMessageHelper : IDisposable
        {
            public IMessage Message { get; set; }
            public AutoResetEvent Trigger { get; private set; }

            public AsyncMessageHelper()
            {
                this.Trigger = new AutoResetEvent(false);
            }

            #region IDisposable Members

            public void Dispose()
            {
                this.Trigger.Close();
                this.Message = null;
            }

            #endregion
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            lock (this)
            {
                this.producer.Dispose();
                this.producer = null;

                if (this.isInitializedForSynchronous)
                {
                    this.responseBuffer.Clear();
                    this.responseBuffer = null;
                    this.responseConsumer.Dispose();
                    this.responseConsumer = null;
                }

                this.session.Dispose();
                this.session = null;
                this.messageFactory = null;
                this.connection = null;
                this.destination = null;
            }
        }

        #endregion
    }
}
