using System;

namespace mooftpserv.lib
{
    /// <summary>
    /// Interface for file system access from FTP.
    /// </summary>
    public interface IFileSystemHandler
    {
        /// <summary>
        /// Clone this instance.
        /// </summary>
        IFileSystemHandler Clone();

        /// <summary>
        /// PWD: Returns the path of the current working directory.
        /// </summary>
        string GetCurrentDirectory();

        /// <summary>
        /// CWD: Changes the current directory.
        /// </summary>
        /// <returns>
        /// Null or an error message.
        /// </returns>
        /// <param name='path'>
        /// A relative or absolute path to which to change.
        /// </param>
        string ChangeCurrentDirectory(string path);

        /// <summary>
        /// LIST: Return a list of files in the current directory, or the optionally specified path.
        /// </summary>
        /// <param name='path'>
        /// An array of filenames.
        /// </param>
        string[] List(string path = null);
    }
}

