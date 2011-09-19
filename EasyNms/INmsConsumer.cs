using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    public interface INmsConsumer
    {
        void Consume(Action<IMessage> messageReceivedCallback);
        void Consume(Func<MessageFactory, IMessage, IMessage> messageReceivedCallback);
    }
}
