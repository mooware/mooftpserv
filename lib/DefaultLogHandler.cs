using System;
using System.IO;
using System.Net;

namespace mooftpserv
{
    public class DefaultLogHandler : ILogHandler
    {
        private TextWriter Out;
        private bool Verbose;

        public DefaultLogHandler(TextWriter Output, bool Verbose)
        {
            this.Out = Output;
            this.Verbose = Verbose;
        }

        public DefaultLogHandler(bool Verbose) : this(Console.Out, Verbose)
        {
        }

        public DefaultLogHandler() : this(false)
        {
        }

        private void Write(IPEndPoint peer, string format, params object[] args)
        {
            string now = DateTime.Now.ToString("HH:mm:ss.fff");
            Out.WriteLine(String.Format("{0}, {1}: {2}", now, peer, String.Format(format, args)));
        }

        public void NewControlConnection(IPEndPoint peer)
        {
            Write(peer, "new control connection");
        }

        public void ClosedControlConnection(IPEndPoint peer)
        {
            Write(peer, "closed control connection");
        }

        public void ReceivedCommand(IPEndPoint peer, string verb, string arguments)
        {
            if (Verbose) {
                string argtext = (arguments == null || arguments == "" ? "" : ' ' + arguments);
                Write(peer, "received command: {0}{1}", verb, argtext);
            }
        }

        public void SentResponse(IPEndPoint peer, uint code, string description)
        {
            if (Verbose)
                Write(peer, "sent response: {0} {1}", code, description);
        }

        public void NewDataConnection(IPEndPoint peer, IPEndPoint remote, IPEndPoint local, bool passive)
        {
            if (Verbose)
                Write(peer, "new data connection: {0} <-> {1} ({2})", remote, local, (passive ? "passive" : "active"));
        }

        public void ClosedDataConnection(IPEndPoint peer, IPEndPoint remote, IPEndPoint local, bool passive)
        {
            if (Verbose)
                Write(peer, "closed data connection: {0} <-> {1} ({2})", remote, local, (passive ? "passive" : "active"));
        }
    }
}

