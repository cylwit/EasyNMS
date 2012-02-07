using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using System.Threading;

namespace EasyNms
{
    public class NmsProducer : IDisposable
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private static volatile int idCounter;

        #region Fields

        private IMessageProducer producer;
        private Dictionary<string, AsyncMessageHelper> responseBuffer;
        private INmsSession session;
        private INmsConnection connection;
        private ITemporaryQueue temporaryQueue;
        private IMessageConsumer responseConsumer;
        private bool isInitializedForSynchronous;
        private IDestination destination;
        private MessageFactory messageFactory;
        private bool isSynchronous;
        private int id;
        private bool isInitialized;
        private MsgDeliveryMode deliveryMode;
        private Destination innerDestination;
        private AutoResetEvent asr = new AutoResetEvent(false);

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

        private NmsProducer()
        {
            this.id = idCounter++;
        }

        private NmsProducer(INmsConnection connection)
            : this()
        {
            this.Setup(connection, MsgDeliveryMode.Persistent, false);
        }

        internal NmsProducer(INmsConnection connection, Destination destination, MsgDeliveryMode deliveryMode = MsgDeliveryMode.Persistent, bool synchronous = false)
            : this()
        {
            this.innerDestination = destination;
            this.Setup(connection, deliveryMode, synchronous);
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
            if (!this.isInitialized)
                this.asr.WaitOne(10000);

            this.producer.Send(destination.GetDestination(this.session), message, deliveryMode, messagePriority, timeToLive);
        }

        public IMessage SendRequestResponse(IMessage message, int timeoutInMilliseconds = 15000)
        {
            var now = DateTime.Now;
            if (!this.isInitialized)
            {
                this.asr.WaitOne(timeoutInMilliseconds);
                if (!this.isInitialized)
                    throw new TimeoutException("Could not send the message because no connections were available within the specified timeout period.");
            }
            

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

                // Calculate the remaining timeout time since we may have had to wait.
                int remainingTime = (int)((DateTime.Now - now).TotalMilliseconds);
                remainingTime = timeoutInMilliseconds - remainingTime;
                remainingTime = (remainingTime < 0) ? 0 : remainingTime;

                // Wait for a response for up to [timeout] seconds.  This blocks until the timeout expires or a message is received (.Set() is called on the trigger then, allowing execution to continue).
                asyncMessageHelper.Trigger.WaitOne(remainingTime, true);

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

        private void Setup(INmsConnection connection, MsgDeliveryMode deliveryMode, bool synchronous)
        {
            this.isSynchronous = synchronous;
            this.deliveryMode = deliveryMode;
            this.connection = connection;
            this.session = connection.GetSession();
            this.messageFactory = new MessageFactory(this.session.InnerSession);
            this.connection.ConnectionInterrupted += new EventHandler<NmsConnectionEventArgs>(connection_ConnectionInterrupted);
            this.connection.ConnectionResumed += new EventHandler<NmsConnectionEventArgs>(connection_ConnectionResumed);

            if (this.innerDestination == null)
            {
                this.producer = this.session.CreateProducer();
            }
            else
            {
                this.destination = this.innerDestination.GetDestination(this.session);
                this.producer = this.session.CreateProducer(this.destination);
            }

            this.producer.DeliveryMode = deliveryMode;

            if (synchronous)
                this.InitializeForSynchronous();

            this.isInitialized = true;
            this.asr.Set();
        }

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
                this.temporaryQueue = this.session.GetTemporaryQueue();
                this.responseConsumer = this.session.CreateConsumer(this.temporaryQueue);
                this.responseConsumer.Listener += new MessageListener(responseConsumer_Listener);
                this.isInitializedForSynchronous = true;
                this.isSynchronous = true;
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

        void connection_ConnectionResumed(object sender, NmsConnectionEventArgs e)
        {
            lock (this)
            {
                if (this.isInitialized)
                    return;

                log.Info("[{0}] Resuming producer #{1} on this connection.", e.Connection.ID, this.id);

                this.connection = (INmsConnection)sender;
                this.Setup(this.connection, this.deliveryMode, this.isSynchronous);
            }
        }

        void connection_ConnectionInterrupted(object sender, NmsConnectionEventArgs e)
        {
            lock (this)
            {
                if (this.connection != sender)
                    return;

                this.isInitialized = false;
                this.isInitializedForSynchronous = false;
                this.asr.Reset();

                log.Warn("[{0}] Producer #{1}'s connection was lost.  Attempting to recreate.", e.Connection.ID, this.id);

                this.producer.Dispose();
                this.producer = null;
                this.session.Dispose();
                this.session = null;

                try
                {
                    this.Setup(this.connection, this.deliveryMode, this.isSynchronous);
                }
                catch (Exception ex)
                {
                    log.Warn("[{0}] Failed to recreate producer using this connection ({1}).  Waiting for resume condition to restart.", e.Connection.ID, ex.Message);
                }
            }
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

                this.connection.ConnectionInterrupted -= new EventHandler<NmsConnectionEventArgs>(connection_ConnectionInterrupted);
                this.connection.ConnectionResumed -= new EventHandler<NmsConnectionEventArgs>(connection_ConnectionResumed);

                this.session.Dispose();
                this.session = null;
                this.messageFactory = null;
                this.connection = null;
                this.destination = null;
                this.asr.Close();
                this.asr = null;
            }
        }

        #endregion
    }
}
