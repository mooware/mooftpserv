using System;
using System.Net;

namespace mooftpserv
{
    /// <summary>
    /// Interface for a logger. Methods should be self-explanatory.
    /// </summary>
    public interface ILogHandler
    {
        void NewControlConnection(IPEndPoint peer);
        void ClosedControlConnection(IPEndPoint peer);
        void ReceivedCommand(IPEndPoint peer, string verb, string arguments);
        void SentResponse(IPEndPoint peer, uint code, string description);
        void NewDataConnection(IPEndPoint peer, IPEndPoint remote, IPEndPoint local, bool passive);
        void ClosedDataConnection(IPEndPoint peer, IPEndPoint remote, IPEndPoint local, bool passive);
    }
}

