using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms.Sessions
{
    public class NmsSession : IDisposable
    {
        #region Fields

        private IConnection connection;
        private ISession session;
        private ITemporaryQueue temporaryQueue;

        internal MessageFactory messageFactory;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the acknowledgement mode for this session.
        /// </summary>
        public AcknowledgementMode @AcknowledgementMode { get; private set; }

        /// <summary>
        /// Gets the underlying ISession.
        /// </summary>
        public ISession Session
        {
            get { return this.session; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new NmsSession instance for the given connection and session.
        /// </summary>
        /// <param name="connection">The connection to which the session belongs.</param>
        /// <param name="session">The session which this NmsSession wraps.</param>
        internal NmsSession(IConnection connection, ISession session)
        {
            this.connection = connection;
            this.session = session;
            this.AcknowledgementMode = session.AcknowledgementMode;
            this.messageFactory = new MessageFactory(this);
        }

        #endregion

        #region Methods [public] [virtual]

        /// <summary>
        /// Disposes of the underlying session.
        /// </summary>
        public virtual void Destroy()
        {
            if (this.session == null)
                return;

            this.session.Dispose();
            this.session = null;
        }

        #endregion
        #region Methods [public]

        /// <summary>
        /// Creates a temporary queue for this session, optionally (and by default) using a previously-created temporary queue for this session.
        /// </summary>
        /// <param name="useCachedQueue">Whether or not to use a previously-created temporary queue for this session.  If no temporary queue has
        /// been created then a new temporary queue will be created and cached.</param>
        /// <returns>The resulting ITemporaryQueue destination.</returns>
        public ITemporaryQueue CreateTemporaryQueue(bool useCachedQueue = true)
        {
            if (useCachedQueue && this.temporaryQueue != null)
                return this.temporaryQueue;

            this.temporaryQueue = this.session.CreateTemporaryQueue();
            return this.temporaryQueue;
        }

        /// <summary>
        /// Resolves the given destination name into an IDestination.
        /// </summary>
        /// <param name="destinationName">The name of the destination.</param>
        /// <returns>The resolved IDestination.</returns>
        public IDestination GetDestination(string destinationName)
        {
            return this.session.GetDestination(destinationName);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Disposes of the underlying session.
        /// </summary>
        public virtual void Dispose()
        {
            this.Destroy();
        }

        #endregion
    }
}
