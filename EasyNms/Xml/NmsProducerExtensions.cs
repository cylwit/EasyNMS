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
        /// <summary>
        /// Sends the XML-serialized input object to the destination queue and blocks until a response has been received.
        /// </summary>
        /// <typeparam name="TIn">The request object type.</typeparam>
        /// <typeparam name="TResult">The response object type.</typeparam>
        /// <param name="producer">The producer used to send the request.</param>
        /// <param name="o">The request object.</param>
        /// <param name="timeoutInMilliseconds">The time (in milliseconds) to wait before a TimeoutException is thrown.  The default timeout is 15000 (15 seconds).</param>
        /// <param name="throwReceivedExceptions">Indicates whether or not received exceptions should be thrown or be passed through to the type check.</param>
        /// <returns>Returns the deserialized response object as a TResult.</returns>
        public static TResult SendXmlRequestResponse<TIn, TResult>(this NmsProducer producer, TIn o, int timeoutInMilliseconds = Constants.DefaultTimeoutMilliseconds, bool throwReceivedExceptions = true)
        {
            var message = producer.MessageFactory.CreateXmlMessage(o);
            var response = producer.SendRequestResponse(message, timeoutInMilliseconds);
            var ret = ((ITextMessage)response).XmlDeserialize();

            if (ret is TResult)
                return (TResult)ret;
            else if (ret is Exception && throwReceivedExceptions)
                throw ((Exception)ret);
            else
                throw new SerializationException("Expected result type '" + typeof(TResult).Name + "' but received type '" + ret.GetType().Name + "'.");
        }

        /// <summary>
        /// Sends the XML-serialized input object to the destination queue and blocks until as response has been received.
        /// </summary>
        /// <param name="producer">The producer used to send the request.</param>
        /// <param name="o">The request object.</param>
        /// <param name="timeoutInMilliseconds">The time (in milliseconds) to wait before a TimeoutException is thrown.  The default timeout is 15000 (15 seconds).</param>
        /// <param name="throwReceivedExceptions">Indicates whether or not received exceptions should be thrown or if they should be returned as response objects.</param>
        /// <returns>Returns the deserialized response object.</returns>
        public static object SendXmlRequestResponse(this NmsProducer producer, object o, int timeoutInMilliseconds = Constants.DefaultTimeoutMilliseconds, bool throwReceivedExceptions = true)
        {
            var message = producer.MessageFactory.CreateXmlMessage(o);
            var response = producer.SendRequestResponse(message, timeoutInMilliseconds);
            var ret = ((ITextMessage)response).XmlDeserialize();
            if (ret is Exception && throwReceivedExceptions)
                throw ((Exception)ret);
            return ret;
        }

        /// <summary>
        /// Sends the XML-serialized input object to the destination queue.
        /// </summary>
        /// <typeparam name="TIn">The request object type.</typeparam>
        /// <param name="producer">The producer used to send the request.</param>
        /// <param name="o">The request object.</param>
        public static void SendXmlRequest<TIn>(this NmsProducer producer, TIn o)
        {
            var message = producer.MessageFactory.CreateXmlMessage(o);
            producer.SendRequest(message);
        }

        /// <summary>
        /// Sends the XML-serialized input object to the destination queue.
        /// </summary>
        /// <param name="producer">The producer used to send the request.</param>
        /// <param name="o">The request object.</param>
        public static void SendXmlRequest(this NmsProducer producer, object o)
        {
            var message = producer.MessageFactory.CreateXmlMessage(o);
            producer.SendRequest(message);
        }
    }
}
