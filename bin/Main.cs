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
        public static void Main(string[] args)
        {
            bool verbose = false;
            int port = -1;
            int buffer = -1;

            // process args
            for (int i = 0; i < args.Length; ++i) {
                if (args[i] == "-h" || args[i] == "--help") {
                    Console.Out.WriteLine("Usage: <program> [-v|--verbose] [-p|--port <port>] [-b|--buffer <kbsize>]");
                    return;
                } else if (args[i] == "-v" || args[i] == "--verbose") {
                    verbose = true;
                } else if (args[i] == "-p" || args[i] == "--port") {
                    if (i == args.Length - 1) {
                        Abort("Too few arguments for {0}", args[i]);
                    }

                    port = ParseNumber(args[i], args[i + 1]);
                    ++i;
                } else if (args[i] == "-b" || args[i] == "--buffer") {
                    if (i == args.Length - 1) {
                        Abort("Too few arguments for {0}", args[i]);
                    }

                    buffer = ParseNumber(args[i], args[i + 1]);
                    ++i;
                } else {
                    Abort("Unknown argument '{0}'", args[i]);
                }
            }

            Server srv = new Server();

            srv.LogHandler = new DefaultLogHandler(verbose);

            if (port != -1)
                srv.LocalPort = port;

            if (buffer != -1)
                srv.BufferSize = buffer;

            Console.Out.WriteLine("Starting server on {0}", srv.LocalEndPoint);

            try {
                srv.Run();
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
            }
        }

        private static void Abort(string format, params object[] arg)
        {
            Console.Error.WriteLine(format, arg);
            Environment.Exit(1);
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

            Abort("Invalid value for '{0}': {1}", option, text);
            return -1;
        }
    }
}
