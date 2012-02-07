using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms
{
    /// <summary>
    /// A message consumer which creates and wraps a specified number of identical consumers for better message processing concurrency 
    /// and easier management.
    /// </summary>
    public class NmsMultiConsumer : IDisposable
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        #region Fields

        private HashSet<NmsConsumer> consumers;

        #endregion

        #region Constructors

        private NmsMultiConsumer()
        {
            this.consumers = new HashSet<NmsConsumer>();
        }

        internal NmsMultiConsumer(INmsConnection connection, Destination destination, int consumerCount, Action<IMessage> messageReceivedCallback, string selector = null)
            : this()
        {
            for (int i = 0; i < consumerCount; i++)
            {
                log.Debug("[{2}] Creating consumer #{0} to destination {1}", i, destination, connection.ID);
                var consumer = new NmsConsumer(connection, destination, messageReceivedCallback, selector);
                this.consumers.Add(consumer);
            }
        }

        internal NmsMultiConsumer(INmsConnection connection, Destination destination, int consumerCount, Func<MessageFactory, IMessage, IMessage> messageReceivedCallback, string selector = null)
            : this()
        {
            for (int i = 0; i < consumerCount; i++)
            {
                log.Debug("[{2}] Creating consumer #{0} to destination {1}", i, destination, connection.ID);
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
