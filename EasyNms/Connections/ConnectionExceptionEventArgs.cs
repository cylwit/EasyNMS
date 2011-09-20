using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms.Connections
{
    public class ConnectionExceptionEventArgs : ConnectionEventArgs
    {
        public Exception @Exception { get; private set; }

        public ConnectionExceptionEventArgs(NmsConnection connection, Exception ex)
            : base(connection)
        {
            this.Exception = ex;
        }
    }
}
