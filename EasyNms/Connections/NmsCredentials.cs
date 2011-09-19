using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms.Connections
{
    public class NmsCredentials : INmsCredentials
    {
        public string Username { get; private set; }
        public string Password { get; private set; }

        public NmsCredentials(string username, string password)
        {
            this.Username = username;
            this.Password = password;
        }
    }
}
