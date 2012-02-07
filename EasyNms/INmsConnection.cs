using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    public interface INmsConnection : IDisposable
    {
        event EventHandler<NmsConnectionEventArgs> ConnectionInterrupted;
        event EventHandler<NmsConnectionEventArgs> ConnectionResumed;

        // General
        void Start();
        void Stop();

        // Properties
        int ID { get; }

        // Sessions
        INmsSession GetSession();
        INmsSession GetSession(AcknowledgementMode acknowledgementMode);

        // Producers
        NmsProducer CreateProducer(Destination destination);
        NmsProducer CreateProducer(Destination destination, MsgDeliveryMode messageDeliveryMode);
        NmsProducer CreateSynchronousProducer(Destination destination);
        NmsProducer CreateSynchronousProducer(Destination destination, MsgDeliveryMode messageDeliveryMode);

        // Consumers
        NmsConsumer CreateConsumer(Destination destination, Action<IMessage> messageReceivedCallback);
        NmsConsumer CreateConsumer(Destination destination, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback);
        NmsMultiConsumer CreateMultiConsumer(Destination destination, int consumerCount, Action<IMessage> messageReceivedCallback);
        NmsMultiConsumer CreateMultiConsumer(Destination destination, int consumerCount, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback);
    }
}
