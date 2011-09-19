using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms.Sessions
{
    public class StandardSessionFactory : SessionFactory
    {
        public override NmsSession CreateSession()
        {
            var newSession = this.connection.CreateSession();
            return new NmsSession(base.connection, newSession);
        }

        public override NmsSession CreateSession(Apache.NMS.AcknowledgementMode acknowledgementMode)
        {
            var newSession = this.connection.CreateSession(acknowledgementMode);
            return new NmsSession(base.connection, newSession);
        }
    }
}
