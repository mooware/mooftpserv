using System;
using mooftpserv;

namespace mooftpserv
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
