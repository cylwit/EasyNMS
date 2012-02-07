using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using Apache.NMS;

namespace EasyNms.Configuration
{
    public class ConnectionPoolElement : ConfigurationElement
    {
        [ConfigurationProperty("endPointManager", IsRequired = true)]
        public EndPointManagerElement EndPointManager
        {
            get { return (EndPointManagerElement)base["endPointManager"]; }
        }

        [ConfigurationProperty("numberOfConnections", IsRequired = true)]
        public int NumberOfConnections
        {
            get { return (int)base["numberOfConnections"]; }
        }

        [ConfigurationProperty("minSessionsPerConnection", IsRequired = true)]
        public int MinSessionsPerConnection
        {
            get { return (int)base["minSessionsPerConnection"]; }
        }

        [ConfigurationProperty("maxSessionsPerConnection", IsRequired = true)]
        public int MaxSessionsPerConnection
        {
            get { return (int)base["maxSessionsPerConnection"]; }
        }

        [ConfigurationProperty("autoGrowSessions", IsRequired = true)]
        public bool AutoGrowSessions
        {
            get { return (bool)base["autoGrowSessions"]; }
        }

        [ConfigurationProperty("endPoints", IsRequired = true)]
        public EndPointElementCollection EndPoints
        {
            get { return (EndPointElementCollection)base["endPoints"]; }
        }

        [ConfigurationProperty("acknowledgementMode", IsRequired = false, DefaultValue = AcknowledgementMode.AutoAcknowledge)]
        public AcknowledgementMode @AcknowledgementMode
        {
            get { return (AcknowledgementMode)base["acknowledgementMode"]; }
        }
    }
}
