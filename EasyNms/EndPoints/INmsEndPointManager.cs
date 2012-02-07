using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms.EndPoints
{
    public interface INmsEndPointManager
    {
        NmsEndPoint GetNextNmsEndPoint();
        void InvalidateEndPoint(NmsEndPoint endPoint);
        void AddEndPoint(NmsEndPoint endPoint);
    }
}
