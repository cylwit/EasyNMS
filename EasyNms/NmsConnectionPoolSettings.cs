using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using EasyNms.EndPoints;

namespace EasyNms
{
    public class NmsConnectionPoolSettings
    {
        public int ConnectionCount { get; set; }
        public int MinimumSessionsPerConnection { get; set; }
        public int MaximumSessionsPerConnection { get; set; }
        public bool AutoGrowSessions { get; set; }
        public NmsCredentials Credentials { get; set; }
        public AcknowledgementMode @AcknowledgementMode { get; set; }
        public IEnumerable<NmsEndPoint> EndPoints { get; set; }

        public NmsConnectionPoolSettings()
        {
            this.ConnectionCount = 1;
            this.MinimumSessionsPerConnection = 10;
            this.MaximumSessionsPerConnection = 50;
            this.AutoGrowSessions = true;
            this.EndPoints = new NmsEndPoint[0];
            this.AcknowledgementMode = Apache.NMS.AcknowledgementMode.AutoAcknowledge;
        }
    }
}