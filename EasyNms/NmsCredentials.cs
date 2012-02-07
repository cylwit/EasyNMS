using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms
{
    public class NmsCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }

        public NmsCredentials(string username, string password)
        {
            this.Username = username;
            this.Password = password;
        }
    }
}
