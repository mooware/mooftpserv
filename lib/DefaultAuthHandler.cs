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
        private bool allowAnyDataPeer;

        private DefaultAuthHandler(IPEndPoint peer, bool allowAnyDataPeer)
        {
          this.peer = peer;
          this.allowAnyDataPeer = allowAnyDataPeer;
        }

        public DefaultAuthHandler(bool allowAnyDataPeer) : this(null, allowAnyDataPeer)
        {
        }

        public DefaultAuthHandler() : this(false)
        {
        }

        public IAuthHandler Clone(IPEndPoint newPeer)
        {
            return new DefaultAuthHandler(newPeer, allowAnyDataPeer);
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
            // allow any peer or only allow active connections to the same peer as the control connection
            return allowAnyDataPeer || peer.Address.Equals(port.Address);
        }
    }
}

