using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms.Sessions
{
    class NmsPooledSession : NmsSession, IDisposable
    {
        internal PooledSessionFactory sessionPool;

        internal NmsPooledSession(IConnection connection, ISession session, PooledSessionFactory pool)
            : base(connection, session)
        {
            this.sessionPool = pool;
        }

        public override void Destroy()
        {
            this.sessionPool = null;
            base.Destroy();
        }

        public override void Dispose()
        {
            this.sessionPool.ReturnSession(this);
        }
    }
}
