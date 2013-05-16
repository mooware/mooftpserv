using System;
using System.Net;

namespace mooftpserv
{
    /// <summary>
    /// Interface for a class managing user authentication and allowing connections.
    /// </summary>
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

        /// <summary>
        /// Check if a control connection from the given peer should be allowed.
        /// </summary>
        bool AllowControlConnection(IPEndPoint peer);

        /// <summary>
        /// Check if the PORT command of the given client with the given
        /// target endpoint should be allowed.
        /// </summary>
        /// <param name='peer'>
        /// The peer of the control connection on which the PORT command was issued.
        /// </param>
        /// <param name='port'>
        /// The argument given by the peer in the PORT command.
        /// </param>
        bool AllowActiveDataConnection(IPEndPoint peer, IPEndPoint target);
    }
}

