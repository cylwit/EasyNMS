using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace EasyNms.Configuration
{
    public class CredentialsElement : ConfigurationElement
    {
        [ConfigurationProperty("username", IsRequired = false)]
        public string Username
        {
            get { return (string)base["username"]; }
        }

        [ConfigurationProperty("password", IsRequired = false)]
        public string Password
        {
            get { return (string)base["password"]; }
        }
    }
}
