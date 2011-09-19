using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms.Sessions
{
    public abstract class SessionFactory : IDisposable
    {
        protected IConnection connection;

        internal void SetConnection(IConnection connection)
        {
            this.connection = connection;
        }

        public abstract NmsSession CreateSession();
        public abstract NmsSession CreateSession(AcknowledgementMode acknowledgementMode);

        public virtual void Start()
        {
        }

        public virtual void Stop()
        {
        }

        public virtual void Dispose()
        {
        }
    }
}
