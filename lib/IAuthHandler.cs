using System;

namespace mooftpserv.lib
{
    public interface IAuthHandler
    {
        /// <summary>
        /// Clone this instance. Each FTP session uses a separate, cloned instance.
        /// </summary>
        IAuthHandler Clone();

        /// <summary>
        /// Check the given login. Note that the method can be called in three ways:
        /// - user and pass are null: anonymous authentication
        /// - pass is null: login only with username (e.g. "anonymous")
        /// - both are non-null: login with user and password
        /// </summary>
        /// <param name='user'>
        /// The username, or null.
        /// </param>
        /// <param name='pass'>
        /// The password, or null.
        /// </param>
        bool AllowLogin(string user, string pass);
    }
}

