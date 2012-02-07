using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    public class MessageFactory
    {
        internal ISession session;

        internal MessageFactory(ISession session)
        {
            this.session = session;
        }

        public ITextMessage CreateTextMessage()
        {
            return this.session.CreateTextMessage();
        }

        public ITextMessage CreateTextMessage(string text)
        {
            return this.session.CreateTextMessage(text);
        }
    }
}
