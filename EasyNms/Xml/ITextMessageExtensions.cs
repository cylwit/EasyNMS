using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using System.Xml.Serialization;
using System.IO;

namespace EasyNms.Xml
{
    public static class ITextMessageExtensions
    {
        public static object XmlDeserialize(this ITextMessage message)
        {
            if (string.IsNullOrEmpty(message.NMSType))
                throw new InvalidOperationException("Cannot deserialize message as XML type: NMSType header is not set.");
            if (!message.NMSType.StartsWith(Constants.XmlHeader))
                throw new InvalidOperationException("Cannot deserialize message as XML type: NMSType header does not begin with the '" + Constants.XmlHeader + "' constant.");

            var type = Type.GetType(message.NMSType.Substring(4, message.NMSType.Length - 4));
            var serializer = new XmlSerializer(type);
            using (var reader = new StringReader(message.Text))
            {
                return serializer.Deserialize(reader);
            }
        }

        public static T XmlDeserializeAs<T>(this ITextMessage message)
        {
            if (string.IsNullOrEmpty(message.NMSType))
                throw new InvalidOperationException("Cannot deserialize message as XML type: NMSType header is not set.");
            if (!message.NMSType.StartsWith(Constants.XmlHeader))
                throw new InvalidOperationException("Cannot deserialize message as XML type: NMSType header does not begin with the '" + Constants.XmlHeader + "' constant.");

            return (T)XmlDeserialize(message);
        }

        public static bool IsXmlMessage(this ITextMessage message)
        {
            return (message != null && !string.IsNullOrEmpty(message.NMSType) && message.NMSType.StartsWith(Constants.XmlHeader));
        }
    }
}
