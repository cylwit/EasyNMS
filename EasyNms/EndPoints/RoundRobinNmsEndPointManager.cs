using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EasyNms.EndPoints
{
    class RoundRobinNmsEndPointManager : INmsEndPointManager
    {
        private List<NmsEndPoint> allEndPoints;
        private List<NmsEndPoint> validEndPoints;
        private List<NmsEndPoint> invalidEndPoints;

        private volatile int index;

        public RoundRobinNmsEndPointManager()
        {
            this.allEndPoints = new List<NmsEndPoint>();
            this.validEndPoints = new List<NmsEndPoint>();
            this.invalidEndPoints = new List<NmsEndPoint>();
        }

        public NmsEndPoint GetNextNmsEndPoint()
        {
            NmsEndPoint ep;
            lock (this)
            {
                if (this.validEndPoints.Count == 0)
                    throw new InvalidOperationException("There are no endpoints available to retrieve an NMS connection from.");

                if (this.index == validEndPoints.Count)
                    this.index = 0;
                ep = this.validEndPoints[this.index];
                this.index++;
            }
            return ep;
        }

        public void InvalidateEndPoint(NmsEndPoint endPoint)
        {
            lock (this)
            {
                if (this.validEndPoints.Contains(endPoint))
                {
                    this.validEndPoints.Remove(endPoint);
                    this.invalidEndPoints.Add(endPoint);
                }
            }
        }

        public void AddEndPoint(NmsEndPoint endPoint)
        {
            lock (this)
            {
                this.allEndPoints.Add(endPoint);
                this.validEndPoints.Add(endPoint);
            }
        }
    }
}
