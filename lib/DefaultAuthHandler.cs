using System;

namespace mooftpserv
{
    /// <summary>
    /// Default auth handler. Accepts user "anonymous", password empty.
    /// </summary>
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

