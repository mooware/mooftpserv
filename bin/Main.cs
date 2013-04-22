using System;
using System.Collections.Generic;
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

            Server srv = new Server("0.0.0.0", port,
                                    new DefaultAuthHandler(),
                                    new DefaultFileSystemHandler(startDir),
                                    new DefaultLogHandler(verbose));

            Console.Out.WriteLine("Starting server on port {0}", port);

            try {
                srv.Run();
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
            }
        }
    }
}
