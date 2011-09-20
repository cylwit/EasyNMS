using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms.Connections
{
    public class ConnectionEventArgs : EventArgs
    {
        public NmsConnection Connection { get; private set; }

        public ConnectionEventArgs(NmsConnection connection)
        {
            this.Connection = connection;
        }
    }
}
