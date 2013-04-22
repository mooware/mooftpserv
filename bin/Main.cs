using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using mooftpserv;

namespace mooftpserv
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            List<string> argList = new List<string>(args);

            int port = 2121;
            try {
                if (argList.Count > 0) {
                    port = int.Parse(argList[0]);
                    argList.RemoveAt(0);
                }
            } catch (Exception) {
                // CF is missing int.TryParse
            }

            bool verbose = false;
            if (argList.Count > 0) {
                int i = argList.IndexOf("-v");
                if (i != -1) {
                    verbose = true;
                    argList.RemoveAt(i);
                }
            }

            DirectoryInfo startDir = new DirectoryInfo(Path.GetFullPath("."));
            try {
                if (argList.Count > 0) {
                  startDir = new DirectoryInfo(Path.GetFullPath(argList[0]));
                }
            }
            catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
            }

            // on WinCE, 0.0.0.0 does not work because for accepted sockets,
            // LocalEndPoint would also say 0.0.0.0 instead of the real IP
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress bindIp = IPAddress.Any;
            foreach (IPAddress ip in host.AddressList) {
                if (!IPAddress.IsLoopback(ip)) {
                    bindIp = ip;
                    break;
                }
            }

            Server srv = new Server(bindIp, port,
                                    new DefaultAuthHandler(),
                                    new DefaultFileSystemHandler(startDir),
                                    new DefaultLogHandler(verbose));

            Console.Out.WriteLine("Starting server on {0}:{1}, hosting {2}", bindIp, port, startDir);

            try {
                srv.Run();
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
            }
        }
    }
}
