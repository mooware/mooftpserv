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
        public DefaultAuthHandler()
        {
        }

        public IAuthHandler Clone()
        {
            return new DefaultAuthHandler();
        }

        public bool AllowLogin(string user, string pass)
        {
            return (user == "anonymous");
        }

        public bool AllowControlConnection(IPEndPoint peer)
        {
            return true;
        }

        public bool AllowActiveDataConnection(IPEndPoint peer, IPEndPoint port)
        {
            // only allow active connections to the same peer as the control connection
            return peer.Address.Equals(port.Address);
        }
    }
}

