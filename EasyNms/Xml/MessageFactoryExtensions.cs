using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using System.Xml.Serialization;
using System.IO;

namespace EasyNms.Xml
{
    public static class MessageFactoryExtensions
    {
        public static ITextMessage CreateXmlMessage(this MessageFactory messageFactory, object o)
        {
            var serializer = new XmlSerializer(o.GetType());
            StringBuilder sb = new StringBuilder(150);
            using (var writer = new StringWriter(sb))
            {
                serializer.Serialize(writer, o);
            }
            var message = messageFactory.CreateTextMessage(sb.ToString());
            message.NMSType = Constants.XmlHeader + o.GetType().AssemblyQualifiedName;
            return message;
        }

        public static object DeserializeXmlMessage(this MessageFactory messageFactory, ITextMessage message)
        {
            return message.XmlDeserialize();
        }

        public static T DeserializeXmlMessage<T>(this MessageFactory messageFactory, ITextMessage message)
        {
            return message.XmlDeserializeAs<T>();
        }

        public static bool IsXmlMessage(this MessageFactory messageFactory, ITextMessage message)
        {
            return message.IsXmlMessage();
        }
    }
}
