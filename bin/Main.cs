using System;
using mooftpserv.lib;

namespace mooftpserv.bin
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            Server srv = new Server("0.0.0.0", 9999);
            srv.Run();
        }
    }
}
