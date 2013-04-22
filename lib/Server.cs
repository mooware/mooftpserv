using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace mooftpserv
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

        public Server(IPAddress host, int port, IAuthHandler auth, IFileSystemHandler filesys, ILogHandler log)
        {
            this.port = port;
            this.host = host;
            this.authHandler = auth;
            this.fsHandler = filesys;
            this.logHandler = log;
            this.sessions = new List<Session>();
        }

        public void Run()
        {
            if (socket == null)
                socket = new TcpListener(host, port);

            socket.Start();

            while (true)
            {
                Socket client = socket.AcceptSocket();
                Session session = new Session(client, authHandler.Clone(), fsHandler.Clone(), logHandler);
                session.Start();
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
