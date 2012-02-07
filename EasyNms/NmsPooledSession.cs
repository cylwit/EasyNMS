using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    internal class NmsPooledSession : NmsSession, IDisposable
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        internal NmsSessionPool sessionPool;
        internal int id;

        internal NmsPooledSession(IConnection connection, ISession session, NmsSessionPool pool)
            : base(connection, session)
        {
            this.sessionPool = pool;
        }

        public override void Destroy()
        {
            log.Info("[{0}] Session #{1} is being destroyed.", this.sessionPool.connection.id, this.id);
            this.sessionPool = null;
            base.Destroy();
        }

        public new void Dispose()
        {
            log.Debug("[{0}] Session #{1} is returning to the pool.", this.sessionPool.connection.id, this.id);
            this.sessionPool.ReturnSession(this);
        }
    }
}
