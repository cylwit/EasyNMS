using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EasyNms.EndPoints;

namespace EasyNms
{
    public class NmsConnectionEventArgs : EventArgs
    {
        public INmsConnection Connection { get; private set; }

        public NmsConnectionEventArgs(INmsConnection connection)
        {
            this.Connection = connection;
        }
    }
}
