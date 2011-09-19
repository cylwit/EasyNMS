using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms.Sessions
{
    public class PooledSessionFactorySettings
    {
        public int MinimumSessionsPerConnection { get; set; }
        public AcknowledgementMode @AcknowledgementMode { get; set; }

        public PooledSessionFactorySettings()
        {
            this.MinimumSessionsPerConnection = 10;
            this.AcknowledgementMode = Apache.NMS.AcknowledgementMode.AutoAcknowledge;
        }
    }
}
