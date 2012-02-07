using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using EasyNms.EndPoints;
using System.Threading;

namespace EasyNms
{
    /// <summary>
    /// Represents a pooled ActiveMQ connection.
    /// </summary>
    internal class NmsPooledConnection : NmsConnection, IDisposable, INmsConnection
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        #region Fields

        private NmsSessionPool sessionPool;

        internal NmsConnectionPoolSettings settings;
        internal NmsConnectionPool connectionPool;
        internal NmsEndPoint endPoint;

        #endregion

        #region Constructors

        public NmsPooledConnection(IConnection connection, AcknowledgementMode acknowledgementMode)
            : base(connection, acknowledgementMode)
        {
        }

        #endregion

        #region Methods [public]

        /// <summary>
        /// Start the underlying connection and the session pool.
        /// </summary>
        public override void Start()
        {
            log.Debug("[{0}] Starting the connection.", this.id);
            base.Start();

            log.Debug("[{0}] Creating the session pool.", this.id);
            this.sessionPool = new NmsSessionPool(this, this.acknowledgementMode, this.settings);

            log.Debug("[{0}] Starting the session pool.", this.id);
            this.sessionPool.Start();
        }

        /// <summary>
        /// Stop the underlying connection and the session pool.
        /// </summary>
        public override void Stop()
        {
            if (this.sessionPool != null)
            {
                log.Debug("[{0}] Stopping the session pool.", this.id);
                this.sessionPool.Stop();
            }

            log.Debug("[{0}] Stopping the underlying NmsConnection.", this.id);
            base.Stop();
        }

        /// <summary>
        /// Create a new session with the specified acknowledgement mode.  If the requested acknowledgement mode isn't the same
        /// as the session pool's default then a new session is created.
        /// </summary>
        /// <param name="acknowledgementMode">Optionally specify the acknowledgement mode for the session.</param>
        /// <returns>An ActiveMQSession instance from the session pool.</returns>
        public override NmsSession CreateSession(AcknowledgementMode acknowledgementMode)
        {
            if (this.sessionPool.AcknowledgementMode == acknowledgementMode)
            {
                log.Debug("[{0}] Borrowing an existing session.", this.id);
                return this.sessionPool.BorrowSession();
            }
            else
            {
                log.Debug("[{0}] Creating a new session.", this.id);
                return base.CreateSession(acknowledgementMode);
            }
        }

        #endregion

        #region Methods [internal]

        /// <summary>
        /// Propertly destroy this instance.
        /// </summary>
        internal override void Destroy()
        {
            log.Debug("[{0}] Destroying this pooled connection.", this.id);
            lock (this)
            {
                this.Stop();
                this.sessionPool.Dispose();
                this.sessionPool = null;
                this.settings = null;

                base.Destroy();
            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Instead of destroying the connection, return it to the pool.
        /// </summary>
        public new void Dispose()
        {
            //log.Debug("[{0}] Returning this connection to the pool.", this.id);
            //this.connectionPool.ReturnConnection(this);
        }

        #endregion
    }
}
