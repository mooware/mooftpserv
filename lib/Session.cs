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
        // monthnames for LIST command, since DateTime returns localized names
        private static string[] MONTHS = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        // response text for initial response. preceeded by application name and version number.
        private static string[] HELLO_TEXT = { "What can I do for you?", "Good day, sir or madam.", "Hey ho let's go!", "The poor man's FTP server." };
        // response text for general ok messages
        private static string[] OK_TEXT = { "Sounds good.", "Barely acceptable.", "Alright, I'll do it..." };
        // Result for FEAT command
        private static string[] FEATURES = { "MDTM", "SIZE" };

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

        // remote data port. null when PASV is used.
        private IPEndPoint dataPort;
        private Socket dataSocket;

        public Session(TcpClient socket, IAuthHandler authHandler, IFileSystemHandler fileSystemHandler)
        {
            this.socket = socket;
            this.authHandler = authHandler;
            this.fsHandler = fileSystemHandler;
            this.stream = socket.GetStream();
            this.recvBuffer = new byte[10240];
            this.recvBytes = 0;
            this.randomTextIndex = new Random();

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
                    Respond(215, "UNIX Type: L8");
                    break;
                }
                case "QUIT":
                {
                    Respond(221, "Bye.");
                    socket.Close();
                    break;
                }
                case "FEAT":
                {
                    Respond(211, "Features:\r\n " + String.Join("\r\n ", FEATURES), true);
                    Respond(211, "Features done.");
                    break;
                }
                case "TYPE":
                {
                    // I don't see what difference the types make,
                    // but the RFC says that I should support them
                    if (arguments == "A" || arguments == "A N") {
                        Respond(200, "Switching to ASCII mode.");
                    } else if (arguments == "I") {
                        Respond(200, "Switching to binary mode.");
                    } else {
                        Respond(500, "Unknown TYPE arguments.");
                    }
                    break;
                }
                case "PORT":
                {
                    IPEndPoint port = ParseAddress(arguments);
                    if (port == null) {
                        Respond(500, "Invalid host-port format.");
                        break;
                    }

                    IPAddress clientIP = ((IPEndPoint) socket.Client.RemoteEndPoint).Address;
                    if (!port.Address.Equals(clientIP)) {
                        Respond(500, "Specified IP differs from client IP");
                        break;
                    }

                    dataPort = port;
                    Respond(200, getRandomText(OK_TEXT));
                    break;
                }
                case "PWD":
                {
                    Respond(257, EscapePath(fsHandler.GetCurrentDirectory()));
                    break;
                }
                case "CWD":
                {
                    string ret = fsHandler.ChangeCurrentDirectory(arguments);
                    if (ret == null)
                        Respond(250, getRandomText(OK_TEXT));
                    else
                        Respond(550, ret);
                    break;
                }
                case "CDUP":
                {
                    string ret = fsHandler.ChangeCurrentDirectory("..");
                    if (ret == null)
                        Respond(250, getRandomText(OK_TEXT));
                    else
                        Respond(550, ret);
                    break;
                }
                case "MKD":
                {
                    string ret = fsHandler.CreateDirectory(arguments);
                    if (ret == null)
                        Respond(250, getRandomText(OK_TEXT));
                    else
                        Respond(550, ret);
                    break;
                }
                case "RMD":
                {
                    string ret = fsHandler.RemoveDirectory(arguments);
                    if (ret == null)
                        Respond(250, getRandomText(OK_TEXT));
                    else
                        Respond(550, ret);
                    break;
                }
                case "MDTM":
                {
                    DateTime? time = fsHandler.GetLastModifiedTime(arguments);
                    if (time != null)
                        Respond(213, FormatTime(time.Value));
                    else
                        Respond(550, "Could not get file modification time.");
                    break;
                }
                case "SIZE":
                {
                    long size = fsHandler.GetFileSize(arguments);
                    if (size > -1)
                        Respond(213, size.ToString());
                    else
                        Respond(550, "Could not get file size.");
                    break;
                }
                case "LIST":
                {
                    FileSystemEntry[] list = fsHandler.ListEntries(arguments);
                    if (list == null) {
                        Respond(500, "Failed to get directory listing.");
                        break;
                    }

                    SendData(FormatDirList(list),
                             150, "Sending directory listing.",
                             226, "Directory listing done.");
                    break;
                }
                default:
                {
                    Respond(500, "Unknown command.");
                    break;
                }
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

        private void Respond(uint code, string desc = null, bool moreFollows = false)
        {
            string response = code.ToString();
            if (desc != null)
                response += (moreFollows ? '-' : ' ') + desc;
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

        private string EscapePath(string path)
        {
            return '"' + path.Replace("\"", "\\\"") + '"';
        }

        private IPEndPoint ParseAddress(string address)
        {
            string[] tokens = address.Split(',');
            byte[] bytes = new byte[tokens.Length];
            for (int i = 0; i < tokens.Length; ++i) {
                if (!byte.TryParse(tokens[i], out bytes[i]))
                    return null;
            }

            long ip = bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24;
            int port = bytes[4] << 8 | bytes[5];
            return new IPEndPoint(ip, port);
        }

        private string FormatAddress(IPEndPoint address)
        {
            byte[] ip = address.Address.GetAddressBytes();
            int port = address.Port;

            return String.Format("{0},{1},{2},{3},{4},{5}",
                                 ip[0], ip[1], ip[2], ip[3],
                                 port & 0xFF00, port & 0x00FF);
        }

        private string FormatTime(DateTime time)
        {
            return time.ToString("yyyyMMddHHmmss");
        }

        private string FormatDirList(FileSystemEntry[] list)
        {
            int maxSizeChars = 0;
            int maxNameChars = 0;
            foreach (FileSystemEntry entry in list) {
                maxSizeChars = Math.Max(maxSizeChars, entry.Size.ToString().Length);
                maxNameChars = Math.Max(maxNameChars, entry.Name.Length);
            }

            DateTime sixMonthsAgo = DateTime.Now.ToUniversalTime().AddMonths(-6);

            string result = "";
            foreach (FileSystemEntry entry in list) {
                char dirflag = (entry.IsDirectory ? 'd' : '-');
                string size = entry.Size.ToString().PadLeft(maxSizeChars);
                string name = entry.Name.PadLeft(maxNameChars);
                string modtime = MONTHS[entry.LastModifiedTime.Month - 1];
                if (entry.LastModifiedTime < sixMonthsAgo)
                    modtime += entry.LastModifiedTime.ToString(" dd  yyyy");
                else
                    modtime += entry.LastModifiedTime.ToString(" dd hh:mm");

                result += String.Format("{0}rwxr--r-- 1 owner group {1} {2} {3}\r\n",
                                        dirflag, size, modtime, name);
            }

            return result;
        }

        private void SendData(string data, uint beforeCode, string beforeDesc, uint afterCode, string afterDesc) {
            if (dataPort == null && dataSocket == null) {
                Respond(425, "No data port configured, use PORT or PASV.");
                return;
            }

            if (dataPort != null) {
                dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                dataSocket.Connect(dataPort);
            } else {
                throw new NotImplementedException();
            }

            Respond(beforeCode, beforeDesc);

            byte[] buf = Encoding.ASCII.GetBytes(data);
            dataSocket.Send(buf);
            dataSocket.Close();
            dataSocket = null;

            Respond(afterCode, afterDesc);
        }

        private string getRandomText(string[] texts)
        {
            int index = randomTextIndex.Next(0, texts.Length);
            return texts[index];
        }
    }
}
