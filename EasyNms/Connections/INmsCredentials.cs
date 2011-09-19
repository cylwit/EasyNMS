using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms.Connections
{
    public interface INmsCredentials
    {
        string Username { get; }
        string Password { get; }
    }
}
