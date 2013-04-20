using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace mooftpserv.lib
{
    public class Session
    {
        // version from AssemblyInfo
        private static string LIB_VERSION = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        // fake responses for SYST command
        private static string[] SYST_TEXT = { "Windows For Workgroups 3.11", "Super Nintendo", "Cheesecake 2003", "MOONIX", "Mooltics", "BS2000" };
        // response text for initial response. preceeded by application name and version number.
        private static string[] HELLO_TEXT = { "What can I do for you?", "Good day, sir or madam.", "Hey ho let's go!", "The poor man's file transfer protocol." };
        // response text for general ok messages
        private static string[] OK_TEXT = { "Sounds good.", "Barely acceptable.", "Alright, I'll do it..." };

        private TcpClient socket;
        private IAuthHandler authHandler;
        private IFileSystemHandler fsHandler;
        private Thread thread;
        private NetworkStream stream;
        private byte[] recvBuffer;
        private int recvBytes;

        private Random randomTextIndex;
        private bool loggedIn = false;
        private string loggedInUser = null;

        public Session(TcpClient socket, IAuthHandler authHandler, IFileSystemHandler fileSystemHandler)
        {
            this.socket = socket;
            this.authHandler = authHandler;
            this.fsHandler = fileSystemHandler;
            this.stream = socket.GetStream();
            this.recvBuffer = new byte[10240];
            this.recvBytes = 0;
            this.randomTextIndex = new Random(socket.Client.Handle.ToInt32());

            this.thread = new Thread(new ThreadStart(this.Work));
            this.thread.Start();
        }

        public bool IsOpen
        {
            get { return thread.IsAlive; }
        }

        public void Close()
        {
            thread.Abort();
            socket.Close();
        }

        private void Work()
        {
            Respond(220, String.Format("This is mooftpserv v{0}. {1}", LIB_VERSION, getRandomText(HELLO_TEXT)));

            // allow anonymous login?
            if (authHandler.AllowLogin(null, null)) {
                loggedIn = true;
            }

            while (socket.Connected) {
                string command = ReadCommand();

                if (command == null) {
                    Respond(500, "Failed to read command, closing connection.");
                    break;
                } else if (command.Trim() == "") {
                    // ignore empty lines
                    continue;
                }

                string[] tokens = command.Split(new char[] { ' ' }, 2);
                string verb = tokens[0].ToUpper(); // commands are case insensitive
                string args = (tokens.Length > 1 ? tokens[1] : null);

                if (loggedIn)
                    ProcessCommand(verb, args);
                else if (verb == "QUIT") { // QUIT should always be allowed
                    Respond(221, "Bye.");
                    socket.Close();
                } else {
                    HandleAuth(verb, args);
                }
            }
        }

        private void ProcessCommand(string verb, string arguments)
        {
            switch (verb) {
                case "SYST":
                {
                    Respond(215, getRandomText(SYST_TEXT));
                    break;
                }
                case "QUIT":
                {
                    Respond(221, "Bye.");
                    socket.Close();
                    break;
                }
                case "PWD":
                    Respond(257, fsHandler.GetCurrentDirectory());
                    break;
                case "CWD":
                    string ret = fsHandler.ChangeCurrentDirectory(arguments);
                    if (ret == null)
                        Respond(250, getRandomText(OK_TEXT));
                    else
                        Respond(550, ret);
                    break;
                default:
                    Respond(500, "Unknown command.");
                    break;
            }
        }

        private string ReadCommand()
        {
            int endPos = -1;
            // can there already be a command in the buffer?
            if (recvBytes > 0)
                Array.IndexOf(recvBuffer, (byte)'\n', 0, recvBytes);

            do {
                int freeBytes = recvBuffer.Length - recvBytes;
                int bytes = stream.Read(recvBuffer, recvBytes, freeBytes);
                recvBytes += bytes;

                // search \r\n
                endPos = Array.IndexOf(recvBuffer, (byte)'\r', 0, recvBytes);
                if (endPos != -1 && (recvBytes <= endPos + 1 || recvBuffer[endPos + 1] != (byte)'\n'))
                    endPos = -1;
            } while (endPos == -1 && recvBytes < recvBuffer.Length);

            if (endPos == -1)
                return null;

            string result = Encoding.ASCII.GetString(recvBuffer, 0, endPos);

            // remove the command from the buffer
            recvBytes -= (endPos + 2);
            Array.Copy(recvBuffer, endPos + 2, recvBuffer, 0, recvBytes);

            return result;
        }

        private void Respond(uint code, string desc = null)
        {
            string response = code.ToString();
            if (desc != null) response += " " + desc;
            response += "\r\n";

            byte[] sendBuffer = Encoding.ASCII.GetBytes(response);
            stream.Write(sendBuffer, 0, sendBuffer.Length);
        }

        private void HandleAuth(string verb, string args)
        {
            if (verb == "USER" && args != null) {
                if (authHandler.AllowLogin(args, null)) {
                    Respond(230, "Login successful.");
                    loggedIn = true;
                } else {
                    loggedInUser = args;
                    Respond(331, "Password please.");
                }
            } else if (verb == "PASS") {
                if (loggedInUser != null) {
                    if (authHandler.AllowLogin(loggedInUser, args)) {
                        Respond(230, "Login successful.");
                        loggedIn = true;
                    } else {
                        loggedInUser = null;
                        Respond(530, "Login failed, please try again.");
                    }
                } else {
                    Respond(530, "No USER specified.");
                }
            } else {
                Respond(530, "Please login first.");
            }
        }

        private string getRandomText(string[] texts)
        {
            int index = randomTextIndex.Next(0, texts.Length);
            return texts[index];
        }
    }
}
