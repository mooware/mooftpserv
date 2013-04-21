using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace mooftpserv
{
    public class Session
    {
        // size of stream buffers
        private static int BUFFER_SIZE = 4096;
        // version from AssemblyInfo
        private static string LIB_VERSION = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        // monthnames for LIST command, since DateTime returns localized names
        private static string[] MONTHS = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        // response text for initial response. preceeded by application name and version number.
        private static string[] HELLO_TEXT = { "What can I do for you?", "Good day, sir or madam.", "Hey ho let's go!", "The poor man's FTP server." };
        // response text for general ok messages
        private static string[] OK_TEXT = { "Sounds good.", "Barely acceptable.", "Alright, I'll do it...", "Consider it done." };
        // Result for FEAT command
        private static string[] FEATURES = { "MDTM", "PASV", "SIZE", "UTF8" };

        private Socket controlSocket;
        private IAuthHandler authHandler;
        private IFileSystemHandler fsHandler;
        private ILogHandler logHandler;
        private Thread thread;

        private Random randomTextIndex;
        private bool loggedIn = false;
        private string loggedInUser = null;
        private string renameFromPath = null;

        // remote data port. null when PASV is used.
        private IPEndPoint dataPort = null;
        private Socket dataSocket = null;
        private IPEndPoint peerEndPoint;
        private byte[] cmdRcvBuffer;
        private int cmdRcvBytes;

        public Session(Socket socket, IAuthHandler authHandler, IFileSystemHandler fileSystemHandler, ILogHandler logHandler)
        {
            this.controlSocket = socket;
            this.authHandler = authHandler;
            this.fsHandler = fileSystemHandler;
            this.logHandler = logHandler;
            this.peerEndPoint = (IPEndPoint) socket.RemoteEndPoint;
            this.cmdRcvBuffer = new byte[BUFFER_SIZE];
            this.cmdRcvBytes = 0;
            this.randomTextIndex = new Random();

            this.thread = new Thread(new ThreadStart(this.Work));
        }

        public bool IsOpen
        {
            get { return thread.IsAlive; }
        }

        public void Start()
        {
            if (!thread.IsAlive)
                this.thread.Start();
        }

        public void Stop()
        {
            if (thread.IsAlive)
                thread.Abort();

            if (controlSocket.Connected)
                controlSocket.Close();

            if (dataSocket != null && dataSocket.Connected)
                dataSocket.Close();
        }

        private void Work()
        {
            logHandler.NewControlConnection(peerEndPoint);
            try {
                Respond(220, String.Format("This is mooftpserv v{0}. {1}", LIB_VERSION, GetRandomText(HELLO_TEXT)));

                // allow anonymous login?
                if (authHandler.AllowLogin(null, null)) {
                    loggedIn = true;
                }

                while (controlSocket.Connected) {
                    string verb;
                    string args;
                    if (!ReadCommand(out verb, out args)) {
                        if (controlSocket.Connected) {
                            // assume clean disconnect if there are no buffered bytes
                            if (cmdRcvBytes != 0)
                                Respond(500, "Failed to read command, closing connection.");
                            controlSocket.Close();
                        }
                        break;
                    } else if (verb.Trim() == "") {
                        // ignore empty lines
                        continue;
                    }

                    try {
                        if (loggedIn)
                            ProcessCommand(verb, args);
                        else if (verb == "QUIT") { // QUIT should always be allowed
                            Respond(221, "Bye.");
                            // first flush, then close
                            controlSocket.Shutdown(SocketShutdown.Both);
                            controlSocket.Close();
                        } else {
                            HandleAuth(verb, args);
                        }
                    } catch (Exception ex) {
                        Respond(500, String.Format("Failed to process command: {0}", ex.Message));
                    }
                }
            } finally {
                if (controlSocket.Connected)
                    controlSocket.Close();

                logHandler.ClosedControlConnection(peerEndPoint);
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
                    // first flush, then close
                    controlSocket.Shutdown(SocketShutdown.Both);
                    controlSocket.Close();
                    break;
                }
                case "USER":
                {
                    Respond(230, "You are already logged in.");
                    break;
                }
                case "PASS":
                {
                    Respond(230, "You are already logged in.");
                    break;
                }
                case "FEAT":
                {
                    Respond(211, "Features:\r\n " + String.Join("\r\n ", FEATURES), true);
                    Respond(211, "Features done.");
                    break;
                }
                case "OPTS":
                {
                    if (arguments == "UTF8 ON")
                        Respond(200, "Always in UTF8 mode.");
                    else
                        Respond(501, "Unknown option.");
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

                    IPAddress clientIP = ((IPEndPoint) controlSocket.RemoteEndPoint).Address;
                    if (!port.Address.Equals(clientIP)) {
                        Respond(500, "Specified IP differs from client IP");
                        break;
                    }

                    dataPort = port;
                    CreateDataSocket(false);
                    Respond(200, GetRandomText(OK_TEXT));
                    break;
                }
                case "PASV":
                {
                    dataPort = null;

                    try {
                        CreateDataSocket(true);
                    } catch (Exception ex) {
                        Respond(500, ex.Message);
                        break;
                    }

                    string port = FormatAddress((IPEndPoint) dataSocket.LocalEndPoint);
                    Respond(200, String.Format("Switched to passive mode ({0})", port));
                    break;
                }
                case "PWD":
                {
                    ResultOrError<string> ret = fsHandler.GetCurrentDirectory();
                    if (ret.HasError)
                        Respond(500, ret.Error);
                    else
                        Respond(257, EscapePath(ret.Result));
                    break;
                }
                case "CWD":
                {
                    ResultOrError<string> ret = fsHandler.ChangeDirectory(arguments);
                    if (ret.HasError)
                        Respond(550, ret.Error);
                    else
                        Respond(200, GetRandomText(OK_TEXT));
                    break;
                }
                case "CDUP":
                {
                    ResultOrError<string> ret = fsHandler.ChangeToParentDirectory();
                    if (ret.HasError)
                        Respond(550, ret.Error);
                    else
                        Respond(200, GetRandomText(OK_TEXT));
                    break;
                }
                case "MKD":
                {
                    ResultOrError<string> ret = fsHandler.CreateDirectory(arguments);
                    if (ret.HasError)
                        Respond(550, ret.Error);
                    else
                        Respond(257, EscapePath(ret.Result));
                    break;
                }
                case "RMD":
                {
                    ResultOrError<bool> ret = fsHandler.RemoveDirectory(arguments);
                    if (ret.HasError)
                        Respond(550, ret.Error);
                    else
                        Respond(250, GetRandomText(OK_TEXT));
                    break;
                }
                case "RETR":
                {
                    ResultOrError<Stream> ret = fsHandler.ReadFile(arguments);
                    if (ret.HasError) {
                        Respond(550, ret.Error);
                        break;
                    }

                    SendData(ret.Result);
                    break;
                }
                case "STOR":
                {
                    ResultOrError<Stream> ret = fsHandler.WriteFile(arguments);
                    if (ret.HasError) {
                        Respond(550, ret.Error);
                        break;
                    }

                    ReceiveData(ret.Result);
                    break;
                }
                case "DELE":
                {
                    ResultOrError<bool> ret = fsHandler.RemoveFile(arguments);
                    if (ret.HasError)
                        Respond(550, ret.Error);
                    else
                        Respond(250, GetRandomText(OK_TEXT));
                    break;
                }
                case "RNFR":
                {
                    if (arguments == null || arguments.Trim() == "") {
                        Respond(500, "Empty path is invalid.");
                        break;
                    }

                    renameFromPath = arguments;
                    Respond(350, "Waiting for target path.");
                    break;
                }
                case "RNTO":
                {
                    if (renameFromPath == null) {
                        Respond(503, "Use RNFR before RNTO.");
                        break;
                    }

                    ResultOrError<bool> ret = fsHandler.RenameFile(renameFromPath, arguments);
                    renameFromPath = null;
                    if (ret.HasError)
                        Respond(550, ret.Error);
                    else
                        Respond(250, GetRandomText(OK_TEXT));
                    break;
                }
                case "MDTM":
                {
                    ResultOrError<DateTime> ret = fsHandler.GetLastModifiedTimeUtc(arguments);
                    if (ret.HasError)
                        Respond(550, ret.Error);
                    else
                        Respond(213, FormatTime(ret.Result));
                    break;
                }
                case "SIZE":
                {
                    ResultOrError<long> ret = fsHandler.GetFileSize(arguments);
                    if (ret.HasError)
                        Respond(550, ret.Error);
                    else
                        Respond(213, ret.Result.ToString());
                    break;
                }
                case "LIST":
                {
                    ResultOrError<FileSystemEntry[]> ret = fsHandler.ListEntries(arguments);
                    if (ret.HasError) {
                        Respond(500, ret.Error);
                        break;
                    }

                    SendData(MakeStream(FormatDirList(ret.Result)));
                    break;
                }
                default:
                {
                    Respond(500, "Unknown command.");
                    break;
                }
            }
        }

        private bool ReadCommand(out string verb, out string args)
        {
            verb = null;
            args = null;

            int endPos = -1;
            // can there already be a command in the buffer?
            if (cmdRcvBytes > 0)
                Array.IndexOf(cmdRcvBuffer, (byte)'\n', 0, cmdRcvBytes);

            do {
                int freeBytes = cmdRcvBuffer.Length - cmdRcvBytes;
                int bytes = controlSocket.Receive(cmdRcvBuffer, cmdRcvBytes, freeBytes, SocketFlags.None);
                if (bytes <= 0)
                    break;

                cmdRcvBytes += bytes;

                // search \r\n
                endPos = Array.IndexOf(cmdRcvBuffer, (byte)'\r', 0, cmdRcvBytes);
                if (endPos != -1 && (cmdRcvBytes <= endPos + 1 || cmdRcvBuffer[endPos + 1] != (byte)'\n'))
                    endPos = -1;
            } while (endPos == -1 && cmdRcvBytes < cmdRcvBuffer.Length);

            if (endPos == -1)
                return false;

            string command = DecodeString(cmdRcvBuffer, endPos);

            // remove the command from the buffer
            cmdRcvBytes -= (endPos + 2);
            Array.Copy(cmdRcvBuffer, endPos + 2, cmdRcvBuffer, 0, cmdRcvBytes);

            string[] tokens = command.Split(new char[] { ' ' }, 2);
            verb = tokens[0].ToUpper(); // commands are case insensitive
            args = (tokens.Length > 1 ? tokens[1] : null);

            logHandler.ReceivedCommand(peerEndPoint, verb, args);

            return true;
        }

        private void Respond(uint code, string desc = null, bool moreFollows = false)
        {
            string response = code.ToString();
            if (desc != null)
                response += (moreFollows ? '-' : ' ') + desc;
            response += "\r\n";

            byte[] sendBuffer = EncodeString(response);
            controlSocket.Send(sendBuffer);

            logHandler.SentResponse(peerEndPoint, code, desc);
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
                                 (port & 0xFF00) >> 8, port & 0x00FF);
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
                string modtime = MONTHS[entry.LastModifiedTimeUtc.Month - 1];
                if (entry.LastModifiedTimeUtc < sixMonthsAgo)
                    modtime += entry.LastModifiedTimeUtc.ToString(" dd  yyyy");
                else
                    modtime += entry.LastModifiedTimeUtc.ToString(" dd hh:mm");

                result += String.Format("{0}rwxr--r-- 1 owner group {1} {2} {3}\r\n",
                                        dirflag, size, modtime, name);
            }

            return result;
        }

        private void SendData(Stream stream)
        {
            try {
                using (Socket socket = OpenDataConnection()) {
                    if (socket == null)
                        return;

                    IPEndPoint remote = (IPEndPoint) socket.RemoteEndPoint;
                    IPEndPoint local = (IPEndPoint) socket.LocalEndPoint;
                    bool passive = (dataPort == null);
                    logHandler.NewDataConnection(peerEndPoint, remote, local, passive);

                    byte[] buffer = new byte[BUFFER_SIZE];
                    try {
                        while (true) {
                            int bytes = stream.Read(buffer, 0, buffer.Length);
                            if (bytes <= 0)
                                break;

                            socket.Send(buffer, bytes, SocketFlags.None);
                        }

                        socket.Shutdown(SocketShutdown.Both);
                        Respond(226, "Transfer complete.");
                    } catch (Exception ex) {
                        Respond(500, ex.Message);
                        return;
                    } finally {
                        logHandler.ClosedDataConnection(peerEndPoint, remote, local, passive);
                    }
                }
            } finally {
                stream.Close();
            }
        }

        private void ReceiveData(Stream stream)
        {
            try {
                using (Socket socket = OpenDataConnection()) {
                    if (socket == null)
                        return;

                    IPEndPoint remote = (IPEndPoint) socket.RemoteEndPoint;
                    IPEndPoint local = (IPEndPoint) socket.LocalEndPoint;
                    bool passive = (dataPort == null);
                    logHandler.NewDataConnection(peerEndPoint, remote, local, passive);

                    try {
                        byte[] buffer = new byte[BUFFER_SIZE];
                        while (true) {
                            int bytes = socket.Receive(buffer);
                            if (bytes < 0) {
                                Respond(500, String.Format("Transfer failed: receive returned {0}", bytes));
                                return;
                            } else if (bytes == 0) {
                                break;
                            }


                            stream.Write(buffer, 0, bytes);
                        }

                        socket.Shutdown(SocketShutdown.Both);
                        Respond(226, "Transfer complete.");
                    } catch (Exception ex) {
                        Respond(500, ex.Message);
                        return;
                    } finally {
                        logHandler.ClosedDataConnection(peerEndPoint, remote, local, passive);
                    }
                }
            } finally {
                stream.Close();
            }
        }

        private void CreateDataSocket(bool listen)
        {
            if (dataSocket != null)
                dataSocket.Close();

            dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

            if (listen) {
                IPAddress serverIP = ((IPEndPoint) controlSocket.LocalEndPoint).Address;
                dataSocket.Bind(new IPEndPoint(serverIP, 0));
                dataSocket.Listen(1);
            }
        }

        private Socket OpenDataConnection()
        {
            if (dataPort == null && !dataSocket.IsBound) {
                Respond(425, "No data port configured, use PORT or PASV.");
                return null;
            }

            Respond(150, "Opening data connection.");

            try {
                if (dataPort != null) {
                    // active mode
                    dataSocket.Connect(dataPort);
                    dataPort = null;
                    return dataSocket;
                } else {
                    // passive mode
                    Socket socket = dataSocket.Accept();
                    dataSocket.Close();
                    return socket;
                }
            } catch (Exception ex) {
                Respond(500, String.Format("Failed to open data connection: {0}", ex.Message));
                return null;
            }
        }

        private string EscapePath(string path)
        {
            return '"' + path.Replace("\"", "\"\"") + '"';
        }

        private byte[] EncodeString(string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }

        private string DecodeString(byte[] data, int len)
        {
            return Encoding.UTF8.GetString(data, 0, len);
        }

        private string DecodeString(byte[] data)
        {
            return DecodeString(data, data.Length);
        }

        private Stream MakeStream(string data)
        {
            return new MemoryStream(EncodeString(data));
        }

        private string GetRandomText(string[] texts)
        {
            int index = randomTextIndex.Next(0, texts.Length);
            return texts[index];
        }
    }
}
