using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EasyNms.Sessions;
using Apache.NMS;

namespace EasyNms
{
    public class MessageFactory
    {
        internal NmsSession session;

        internal MessageFactory(NmsSession session)
        {
            this.session = session;
        }

        public ITextMessage CreateTextMessage(string text = null)
        {
            return (text == null)
                ? this.session.Session.CreateTextMessage()
                : this.session.Session.CreateTextMessage(text);
        }

        public IBytesMessage CreateBytesMessage(byte[] body = null)
        {
            return (body == null)
                ? this.session.Session.CreateBytesMessage()
                : this.session.Session.CreateBytesMessage(body);
        }
    }
}
