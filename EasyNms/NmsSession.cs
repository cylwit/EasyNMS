using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    public class NmsSession : IDisposable, INmsSession
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        #region Fields

        private IConnection connection;
        private ISession session;
        private ITemporaryQueue temporaryQueue;

        #endregion

        #region Properties

        public AcknowledgementMode @AcknowledgementMode { get; private set; }
        public ISession Session
        {
            get { return this.session; }
        }
        public MessageFactory @MessageFactory { get; private set; }
        public ISession InnerSession { get; private set; }

        #endregion

        #region Constructors

        public NmsSession(IConnection connection, ISession session)
        {
            this.connection = connection;
            this.session = session;
            this.AcknowledgementMode = session.AcknowledgementMode;
            this.InnerSession = session;
            this.MessageFactory = new MessageFactory(session);
        }

        #endregion

        #region Methods [public] [virtual]

        public virtual void Destroy()
        {
            if (this.session == null)
                return;

            if (this.temporaryQueue != null)
                this.temporaryQueue.Delete();
            this.temporaryQueue = null;
            this.session.Dispose();
            this.session = null;

            this.connection = null;
        }

        #endregion
        #region Methods [public]

        /// <summary>
        /// Creates a temporary queue for this session, optionally (and by default) using a previously-created temporary queue for this session.
        /// </summary>
        /// <param name="useCachedQueue">Whether or not to use a previously-created temporary queue for this session.  If no temporary queue has
        /// been created then a new temporary queue will be created and cached.</param>
        /// <returns>The resulting ITemporaryQueue destination.</returns>
        [Obsolete("Use GetTemporaryQueue() instead.")]
        public ITemporaryQueue CreateTemporaryQueue(bool useCachedQueue = true)
        {
            if (useCachedQueue && this.temporaryQueue != null)
                return this.temporaryQueue;

            this.temporaryQueue = this.session.CreateTemporaryQueue();
            return this.temporaryQueue;
        }

        public IDestination GetDestination(string destinationName)
        {
            return this.session.GetDestination(destinationName);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            this.Destroy();
        }

        #endregion

        #region INmsSession Members

        public ITemporaryQueue GetTemporaryQueue()
        {
            if (this.temporaryQueue != null)
                return this.temporaryQueue;

            this.temporaryQueue = this.session.CreateTemporaryQueue();
            return this.temporaryQueue;
        }

        public IMessageConsumer CreateConsumer(IDestination destination)
        {
            return this.session.CreateConsumer(destination);
        }

        public IMessageConsumer CreateConsumer(IDestination destination, string selector)
        {
            return this.session.CreateConsumer(destination, selector);
        }

        public IMessageProducer CreateProducer()
        {
            return this.session.CreateProducer();
        }

        public IMessageProducer CreateProducer(IDestination destination)
        {
            return this.session.CreateProducer(destination);
        }

        #endregion
    }
}
