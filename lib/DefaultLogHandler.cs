using System;
using System.IO;
using System.Net;

namespace mooftpserv
{
    /// <summary>
    /// Default log handler.
    ///
    /// Writes to stdout by default. Writes messages for every event
    /// in Verbose mode, otherwise only new and closed control connections.
    /// </summary>
    public class DefaultLogHandler : ILogHandler
    {
        private IPEndPoint peer;
        private TextWriter writer;
        private bool verbose;

        public DefaultLogHandler(TextWriter writer, bool verbose)
        {
            this.writer = writer;
            this.verbose = verbose;
        }

        public DefaultLogHandler(bool verbose) : this(Console.Out, verbose)
        {
        }

        public DefaultLogHandler() : this(false)
        {
        }

        private DefaultLogHandler(IPEndPoint peer, TextWriter writer, bool verbose)
        {
            this.peer = peer;
            this.writer = writer;
            this.verbose = verbose;
        }

        public ILogHandler Clone(IPEndPoint peer)
        {
            return new DefaultLogHandler(peer, writer, verbose);
        }

        private void Write(string format, params object[] args)
        {
            string now = DateTime.Now.ToString("HH:mm:ss.fff");
            writer.WriteLine(String.Format("{0}, {1}: {2}", now, peer, String.Format(format, args)));
        }

        public void NewControlConnection()
        {
            Write("new control connection");
        }

        public void ClosedControlConnection()
        {
            Write("closed control connection");
        }

        public void ReceivedCommand(string verb, string arguments)
        {
            if (verbose) {
                string argtext = (arguments == null || arguments == "" ? "" : ' ' + arguments);
                Write("received command: {0}{1}", verb, argtext);
            }
        }

        public void SentResponse(uint code, string description)
        {
            if (verbose)
                Write("sent response: {0} {1}", code, description);
        }

        public void NewDataConnection(IPEndPoint remote, IPEndPoint local, bool passive)
        {
            if (verbose)
                Write("new data connection: {0} <-> {1} ({2})", remote, local, (passive ? "passive" : "active"));
        }

        public void ClosedDataConnection(IPEndPoint remote, IPEndPoint local, bool passive)
        {
            if (verbose)
                Write("closed data connection: {0} <-> {1} ({2})", remote, local, (passive ? "passive" : "active"));
        }
    }
}

