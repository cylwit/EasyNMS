using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EasyNms.Connections;
using System.Threading;
using EasyNms.Sessions;
using Apache.NMS;
using System.Threading.Tasks;

namespace EasyNms.Tests.Console1
{
    class Program
    {
        const string URI = "activemq:ssl://localhost:61617?keepAlive=true&transport.acceptInvalidBrokerCert=true";
        const string QUEUE = "example.A";
        const int SESSIONS_PER_CONNECTION = 500;
        const int POOLED_CONSUMER_COUNT = 100;
        const int PARALLEL_PRODUCE_COUNT = 100;

        static void Main(string[] args)
        {
            // Setup and start the connection using a pooled connection.
            using (var connection = new NmsConnection(new Uri(URI))
                .SessionFactory(new PooledSessionFactory(new PooledSessionFactorySettings() { MinimumSessionsPerConnection = SESSIONS_PER_CONNECTION }))
                .Start())
            {

                // Create a pooled consumer.  We'll set the number of consumers to 10, which means we can process 10 messages at a time.
                connection.CreatePooledConsumer(QUEUE, POOLED_CONSUMER_COUNT, (msg) =>
                    {
                        // This action will be invoked for each received message.  We'll sleep for 3 seconds each message so that we can
                        // see clearly that messages are truly being processed in parallel.
                        Thread.Sleep(3000);
                        Console.WriteLine("[{0:HH:mm:ss.fffff}] Received => {1}", DateTime.Now, ((ITextMessage)msg).Text);
                    });



                // Create a producer.  We'll use this producer to send 100 test messages to the queue as fast as we can, and then we can watch
                // the consumer consume them.
                using (var producer = connection.CreateProducer(QUEUE))
                {
                    Parallel.For(0, PARALLEL_PRODUCE_COUNT, (i) =>
                        {
                            var msg = producer.MessageFactory.CreateTextMessage("Test #" + i);
                            producer.SendRequest(msg, Apache.NMS.MsgDeliveryMode.NonPersistent, Apache.NMS.MsgPriority.High, new TimeSpan(0, 1, 0));
                            Console.WriteLine("[{0:HH:mm:ss.fffff}] Sent => {1}", DateTime.Now, msg.Text);
                        });
                }



                // So that we don't dispose the connection before the test is run :-)
                Console.ReadLine();
            }
        }
    }
}
