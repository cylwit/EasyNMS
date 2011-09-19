using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EasyNms.Connections;
using Apache.NMS;

namespace EasyNms.Sessions
{
    public class PooledSessionFactory : SessionFactory
    {
        private Queue<NmsPooledSession> idleSessions;
        private HashSet<NmsPooledSession> referencedSessions;
        private PooledSessionFactorySettings settings;

        public bool IsStarted { get; private set; }
        public bool IsDisposed { get; private set; }

        public PooledSessionFactory(PooledSessionFactorySettings settings)
        {
            this.idleSessions = new Queue<NmsPooledSession>();
            this.referencedSessions = new HashSet<NmsPooledSession>();
            this.settings = settings;
        }

        #region Methods [public]

        public override void Start()
        {
            lock (this.idleSessions)
            {
                for (int i = 0; i < this.settings.MinimumSessionsPerConnection; i++)
                {
                    var newSession = base.connection.CreateSession(this.settings.AcknowledgementMode);
                    var session = new NmsPooledSession(base.connection, newSession, this);
                    this.idleSessions.Enqueue(session);
                }
            }
            this.IsStarted = true;
        }

        public override void Stop()
        {
            lock (this)
                this.InnerStop();
        }

        public override NmsSession CreateSession()
        {
            NmsPooledSession session = null;
            lock (this.idleSessions)
                session = this.idleSessions.Dequeue();
            lock (this.referencedSessions)
                this.referencedSessions.Add(session);

            return session;
        }

        public override NmsSession CreateSession(AcknowledgementMode acknowledgementMode)
        {
            throw new InvalidOperationException("Cannot specify the acknowledgement mode on a per-session basis when using PooledSessionFactory.  This must be set on the PooledSessionFactorySettings passed in the constructor.");
        }

        public void ReturnSession(NmsSession session)
        {
            var pooledSession = session as NmsPooledSession;

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

        public override void Dispose()
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
