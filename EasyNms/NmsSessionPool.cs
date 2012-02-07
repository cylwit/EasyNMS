using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    internal class NmsSessionPool : IDisposable
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private static volatile int sessionID;

        internal NmsConnection connection;
        private NmsConnectionPoolSettings settings;
        private Queue<NmsPooledSession> idleSessions;
        private HashSet<NmsPooledSession> referencedSessions;

        public bool IsStarted { get; private set; }
        public bool IsDisposed { get; private set; }
        public AcknowledgementMode AcknowledgementMode { get; private set; }

        public NmsSessionPool(NmsConnection connection, NmsConnectionPoolSettings settings)
            : this(connection, connection.acknowledgementMode, settings)
        {
        }

        public NmsSessionPool(NmsConnection connection, AcknowledgementMode acknowledgementMode, NmsConnectionPoolSettings settings)
        {
            this.connection = connection;
            this.AcknowledgementMode = acknowledgementMode;
            this.idleSessions = new Queue<NmsPooledSession>();
            this.referencedSessions = new HashSet<NmsPooledSession>();
            this.settings = settings;
        }

        #region Methods [public]

        public void Start()
        {
            log.Info("[{0}] Session pool is starting.", this.connection.id);

            lock (this.idleSessions)
            {
                for (int i = 0; i < this.settings.MinimumSessionsPerConnection; i++)
                {
                    sessionID++;

                    log.Info("[{0}] Creating a new session #{1}.", this.connection.id, sessionID);
                    var newSession = this.connection.connection.CreateSession(this.AcknowledgementMode);
                    var session = new NmsPooledSession(this.connection.connection, newSession, this);
                    session.sessionPool = this;
                    session.id = sessionID;

                    this.idleSessions.Enqueue(session);
                }
            }
            this.IsStarted = true;
        }

        public void Stop()
        {
            lock (this)
                this.InnerStop();
        }

        public NmsSession BorrowSession()
        {
            NmsPooledSession session = null;
            lock (this)
            {
                if (this.idleSessions.Count == 0 && this.settings.AutoGrowSessions && this.referencedSessions.Count < this.settings.MaximumSessionsPerConnection)
                {
                    var newSession = this.connection.connection.CreateSession(this.AcknowledgementMode);
                    session = new NmsPooledSession(this.connection.connection, newSession, this);
                }
                else if (this.idleSessions.Count > 0)
                {
                    session = this.idleSessions.Dequeue();
                }
                else
                {
                    log.Warn("[{0}] The maximum number of sessions ({1}) has been reached for the session pool.", this.connection.id, this.settings.MaximumSessionsPerConnection);
                    throw new InvalidOperationException("The maximum number of allowed sessions for the session pool has been reached.");
                }

                this.referencedSessions.Add(session);
            }

            log.Debug("[{0}] Borrowing session #{1}.", this.connection.id, session.id);
            return session;
        }

        public void ReturnSession(NmsSession session)
        {
            var pooledSession = session as NmsPooledSession;

            log.Debug("[{0}] Returning session #{1} to the pool.", this.connection.id, pooledSession.id);

            if (session == null || pooledSession.sessionPool != this)
                throw new InvalidOperationException("Tried to return a session to a pool which it does not belong to.");

            lock (this.referencedSessions)
                this.referencedSessions.Remove(pooledSession);
            lock (this.idleSessions)
                this.idleSessions.Enqueue(pooledSession);
        }

        #endregion

        #region Methods [private]

        private void InnerStop()
        {
            foreach (var session in this.idleSessions)
                session.Destroy();
            foreach (var session in this.referencedSessions)
                session.Destroy();

            this.idleSessions.Clear();
            this.referencedSessions.Clear();

            this.IsStarted = false;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            lock (this)
            {
                this.InnerStop();

                this.connection = null;
                this.settings = null;
                this.idleSessions = null;
                this.referencedSessions = null;

                this.IsDisposed = true;
            }
        }

        #endregion
    }
}
