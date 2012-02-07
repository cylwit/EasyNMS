using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace EasyNms.Configuration
{
    public class EndPointElement : ConfigurationElement
    {
        [ConfigurationProperty("uri")]
        public Uri @Uri
        {
            get { return (Uri)base["uri"]; }
        }

        [ConfigurationProperty("credentials", IsRequired = false, DefaultValue = null)]
        public CredentialsElement Credentials
        {
            get { return (CredentialsElement)base["credentials"]; }
        }
    }
}
