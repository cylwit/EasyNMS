using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace EasyNms.Configuration
{
    public class EasyNmsSection : ConfigurationSection
    {
        [ConfigurationProperty("connectionPool", IsRequired = true)]
        public ConnectionPoolElement ConnectionPool
        {
            get { return (ConnectionPoolElement)base["connectionPool"]; }
        }
    }
}
