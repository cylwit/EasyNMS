using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using System.Runtime.Serialization;

namespace EasyNms.Xml
{
    public static class NmsProducerExtensions
    {
        public static TResult SendXmlRequestResponse<TIn, TResult>(this NmsProducer producer, TIn o, int timeoutInMilliseconds = 15000)
        {
            var message = producer.MessageFactory.CreateXmlMessage(o);
            var response = producer.SendRequestResponse(message, timeoutInMilliseconds);
            var ret = producer.MessageFactory.DeserializeXmlMessage((ITextMessage)response);
            if (ret is Exception)
                throw ((Exception)ret);
            else if (ret is TResult)
                return (TResult)ret;
            else
                throw new SerializationException("Expected result type '" + typeof(TResult).Name + "' but received type '" + ret.GetType().Name + "'.");
        }

        public static void SendXmlRequest<TIn>(this NmsProducer producer, TIn o)
        {
            var message = producer.MessageFactory.CreateXmlMessage(o);
            producer.SendRequest(message);
        }
    }
}
