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

        private void Write(string format, params object[] args)
        {
            string now = DateTime.Now.ToString("HH:mm:ss.fff ");
            Out.WriteLine(now + String.Format(format, args));
        }

        public void NewControlConnection(IPEndPoint peer)
        {
            Write("new control connection from {0}", peer);
        }

        public void ClosedControlConnection(IPEndPoint peer)
        {
            Write("closed control connection to {0}", peer);
        }

        public void ReceivedCommand(IPEndPoint peer, string verb, string arguments)
        {
            if (Verbose) {
                string argtext = (arguments == null || arguments == "" ? "" : ' ' + arguments);
                Write("received command from {0}: {1}{2}", peer, verb, argtext);
            }
        }

        public void SentResponse(IPEndPoint peer, uint code, string description)
        {
            if (Verbose)
                Write("sent response to {0}: {1} {2}", peer, code, description);
        }

        public void NewDataConnection(IPEndPoint peer, IPEndPoint remote, IPEndPoint local, bool passive)
        {
            if (Verbose)
                Write("new data connection from {0}: {1} <-> {2} ({3})", peer, remote, local, (passive ? "passive" : "active"));
        }

        public void ClosedDataConnection(IPEndPoint peer, IPEndPoint remote, IPEndPoint local, bool passive)
        {
            if (Verbose)
                Write("closed data connection to {0}: {1} <-> {2} ({3})", peer, remote, local, (passive ? "passive" : "active"));
        }
    }
}

