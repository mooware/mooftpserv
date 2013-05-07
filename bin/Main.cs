using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using mooftpserv;

namespace mooftpserv
{
    class MainClass
    {
        public static int Main(string[] args)
        {
            bool verbose = false;
            int port = -1;
            int buffer = -1;

            // process args
            for (int i = 0; i < args.Length; ++i) {
                if (args[i] == "-h" || args[i] == "--help") {
                    Console.Out.WriteLine("Usage: <program> [-v|--verbose] [-p|--port <port>] [-b|--buffer <kbsize>]");
                    return 0;
                } else if (args[i] == "-v" || args[i] == "--verbose") {
                    verbose = true;
                } else if (args[i] == "-p" || args[i] == "--port") {
                    if (i == args.Length - 1) {
                        Console.Error.WriteLine("Too few arguments for {0}", args[i]);
                        return 1;
                    }

                    port = ParseNumber(args[i], args[i + 1]);
                    if (port == -1) {
                        Console.Error.WriteLine("Invalid value for '{0}': {1}", args[i], args[i + 1]);
                        return 1;
                    }

                    ++i;
                } else if (args[i] == "-b" || args[i] == "--buffer") {
                    if (i == args.Length - 1) {
                        Console.Error.WriteLine("Too few arguments for {0}", args[i]);
                        return 1;
                    }

                    buffer = ParseNumber(args[i], args[i + 1]);
                    if (buffer == -1) {
                        Console.Error.WriteLine("Invalid value for '{0}': {1}", args[i], args[i + 1]);
                        return 1;
                    }

                    ++i;
                } else {
                    Console.Error.WriteLine("Unknown argument '{0}'", args[i]);
                    return 1;
                }
            }

            Server srv = new Server();

            srv.LogHandler = new DefaultLogHandler(verbose);

            if (port != -1)
                srv.LocalPort = port;

            if (buffer != -1)
                srv.BufferSize = buffer * 1024; // in KB

            Console.Out.WriteLine("Starting server on {0}", srv.LocalEndPoint);

            try {
                srv.Run();
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            return 0;
        }

        private static int ParseNumber(string option, string text)
        {
            try {
                // CF does not have int.TryParse
                int num = int.Parse(text);
                if (num > 0)
                    return num;
            } catch (Exception) {
            }

            return -1;
        }
    }
}
