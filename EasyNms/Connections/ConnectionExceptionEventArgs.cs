using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms.Connections
{
    public class ConnectionExceptionEventArgs : EventArgs
    {
        public Exception @Exception { get; private set; }

        public ConnectionExceptionEventArgs(Exception ex)
        {
            this.Exception = ex;
        }
    }
}
