using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    public interface INmsSession : IDisposable
    {
        MessageFactory @MessageFactory { get; }
        ISession InnerSession { get; }

        // Misc
        ITemporaryQueue GetTemporaryQueue();
        IDestination GetDestination(string destinationName);

        // Consumers
        IMessageConsumer CreateConsumer(IDestination destination);
        IMessageConsumer CreateConsumer(IDestination destination, string selector);

        // Producers
        IMessageProducer CreateProducer();
        IMessageProducer CreateProducer(IDestination destination);
    }
}
