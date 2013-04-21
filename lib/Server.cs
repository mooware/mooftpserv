using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace mooftpserv.lib
{
    public class Server
    {
        private int port;
        private IPAddress host;
        private TcpListener socket;
        private IAuthHandler authHandler;
        private IFileSystemHandler fsHandler;
        private ILogHandler logHandler;
        private List<Session> sessions;

        public Server(string host, int port)
        {
            this.port = port;
            this.host = IPAddress.Parse(host);
            this.sessions = new List<Session>();
        }

        public void Run()
        {
            if (authHandler == null)
                authHandler = new DefaultAuthHandler();

            if (fsHandler == null)
                fsHandler = new DefaultFileSystemHandler(new DirectoryInfo(Directory.GetCurrentDirectory()));

            if (logHandler == null)
                logHandler = new DefaultLogHandler(true);

            if (socket == null)
                socket = new TcpListener(host, port);

            socket.Start();

            while (true)
            {
                TcpClient client = socket.AcceptTcpClient();
                Session session = new Session(client, authHandler.Clone(), fsHandler.Clone(), logHandler);
                sessions.Add(session);

                // purge old sessions
                for (int i = sessions.Count - 1; i >= 0; --i)
                {
                    if (!sessions[i].IsOpen) {
                        sessions.RemoveAt(i);
                        --i;
                    }
                }
            }
        }

        public void Stop()
        {
        }
    }
}
