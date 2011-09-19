using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EasyNms.Connections;
using Apache.NMS;

namespace EasyNms
{
    /// <summary>
    /// A message consumer which creates and wraps a specified number of identical consumers for better message processing concurrency 
    /// and easier management.
    /// </summary>
    public class NmsPooledConsumer : IDisposable
    {
        #region Fields

        private HashSet<NmsConsumer> consumers;

        #endregion

        #region Constructors

        private NmsPooledConsumer()
        {
            this.consumers = new HashSet<NmsConsumer>();
        }

        internal NmsPooledConsumer(NmsConnection connection, Destination destination, int consumerCount, Action<IMessage> messageReceivedCallback, string selector = null)
            : this()
        {
            for (int i = 0; i < consumerCount; i++)
            {
                var consumer = new NmsConsumer(connection, destination, messageReceivedCallback, selector);
                this.consumers.Add(consumer);
            }
        }

        internal NmsPooledConsumer(NmsConnection connection, Destination destination, int consumerCount, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
            : this()
        {
            for (int i = 0; i < consumerCount; i++)
            {
                var consumer = new NmsConsumer(connection, destination, messageReceivedCallback, selector);
                this.consumers.Add(consumer);
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            foreach (var consumer in this.consumers)
                consumer.Dispose();
            this.consumers.Clear();
            this.consumers = null;
        }

        #endregion
    }
}
