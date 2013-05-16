using System;
using System.Net;

namespace mooftpserv
{
    /// <summary>
    /// Default auth handler. Accepts user "anonymous", password empty.
    /// Allows all connections.
    /// </summary>
    public class DefaultAuthHandler : IAuthHandler
    {
        private IPEndPoint peer;

        public DefaultAuthHandler()
        {
        }

        private DefaultAuthHandler(IPEndPoint peer)
        {
          this.peer = peer;
        }

        public IAuthHandler Clone(IPEndPoint peer)
        {
            return new DefaultAuthHandler(peer);
        }

        public bool AllowLogin(string user, string pass)
        {
            return (user == "anonymous");
        }

        public bool AllowControlConnection()
        {
            return true;
        }

        public bool AllowActiveDataConnection(IPEndPoint port)
        {
            // only allow active connections to the same peer as the control connection
            return peer.Address.Equals(port.Address);
        }
    }
}

