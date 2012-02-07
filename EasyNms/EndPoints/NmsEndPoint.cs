using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;

namespace EasyNms.EndPoints
{
    public class NmsEndPoint
    {
        private List<NmsConnection> connections;
        private List<NmsConnection> upConnections;
        private List<NmsConnection> downConnections;

        public Uri @Uri { get; private set; }
        public IConnectionFactory ConnectionFactory { get; private set; }
        public NmsCredentials Credentials { get; private set; }

        #region Constructors

        public NmsEndPoint(string uri, NmsCredentials credentials)
            : this(new Uri(uri), credentials)
        {
        }

        public NmsEndPoint(Uri uri, NmsCredentials credentials)
        {
            this.Credentials = credentials;
            this.Uri = uri;
            this.ConnectionFactory = NMSConnectionFactory.CreateConnectionFactory(uri);
        }

        #endregion

        #region Methods [public]

        public NmsConnection CreateConnection(AcknowledgementMode acknowledgementMode)
        {
            var conn = this.Credentials == null
                ? this.ConnectionFactory.CreateConnection()
                : this.ConnectionFactory.CreateConnection(this.Credentials.Username, this.Credentials.Password);
            return new NmsConnection(conn);
        }

        public NmsConnection CreateConnection()
        {
            return this.CreateConnection(AcknowledgementMode.AutoAcknowledge);
        }

        internal NmsPooledConnection CreatePooledConnection(AcknowledgementMode acknowledgementMode)
        {
            var conn = this.Credentials == null
                ? this.ConnectionFactory.CreateConnection()
                : this.ConnectionFactory.CreateConnection(this.Credentials.Username, this.Credentials.Password);
            return new NmsPooledConnection(conn, AcknowledgementMode.AutoAcknowledge);
        }

        internal NmsPooledConnection CreatePooledConnection()
        {
            return this.CreatePooledConnection(AcknowledgementMode.AutoAcknowledge);
        }

        #endregion
    }
}
