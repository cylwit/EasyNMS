using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using EasyNms.EndPoints;

namespace EasyNms.Configuration
{
    public class EndPointManagerElement : ConfigurationElement
    {
        [ConfigurationProperty("type", IsRequired = false, DefaultValue = "EasyNms.EndPoints.RoundRobinNmsEndPointManager, EasyNms")]
        public string Type
        {
            get { return (string)base["type"]; }
        }

        [ConfigurationProperty("properties", IsRequired = false)]
        public new PropertyElementCollection Properties
        {
            get { return (PropertyElementCollection)base["properties"]; }
        }
    }
}
