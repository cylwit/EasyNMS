using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    /// <summary>
    /// Convenience class to help reduce the number of overloads required between IDestination and a string destination name.
    /// </summary>
    public class Destination
    {
        internal IDestination destination;
        internal string destinationName;
        internal DestinationType destinationType;

        public Destination(IDestination destination)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");

            this.destination = destination;
            this.destinationType = DestinationType.Destination;
        }

        public Destination(string destinationName)
        {
            if (string.IsNullOrEmpty(destinationName))
                throw new ArgumentException("Argument 'destination' cannot be null, empty or whitespace.");

            this.destinationName = destinationName;
            this.destinationType = DestinationType.DestinationName;
        }

        internal IDestination GetDestination(INmsSession session)
        {
            switch (this.destinationType)
            {
                case DestinationType.Destination: return this.destination;
                case DestinationType.DestinationName: return session.GetDestination(this.destinationName);
                default: throw new NotImplementedException("Could not get destination.");
            }
        }

        public static implicit operator Destination(string destinationName)
        {
            return new Destination(destinationName);
        }

        public override string ToString()
        {
            switch (this.destinationType)
            {
                case DestinationType.Destination:
                    return this.destination.ToString();
                case DestinationType.DestinationName:
                    return this.destinationName;
                default:
                    return null;
            }
        }
    }
}
