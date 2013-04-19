using System;

namespace mooftpserv.lib
{
    public class DefaultAuthHandler : IAuthHandler
    {
        public DefaultAuthHandler()
        {
        }

        public IAuthHandler Clone()
        {
            return new DefaultAuthHandler();
        }

        public bool AllowLogin(string user, string pass)
        {
            return (user == "anonymous");
        }
    }
}

