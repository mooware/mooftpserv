using System;
using System.IO;

namespace mooftpserv
{
    /// <summary>
    /// File system entry as returned by List.
    /// </summary>
    public struct FileSystemEntry
    {
        public string Name;
        public bool IsDirectory;
        public long Size;
        public DateTime LastModifiedTimeUtc;
    }

    /// <summary>
    /// Interface for file system access from FTP.
    /// </summary>
    public interface IFileSystemHandler
    {
        /// <summary>
        /// Clone this instance. Each FTP session uses a separate, cloned instance.
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
        /// MKD: Create a directory.
        /// </summary>
        /// <returns>
        /// Null or an error message.
        /// </returns>
        /// <param name='path'>
        /// A relative or absolute path for the new directory.
        /// </param>
        string CreateDirectory(string path);

        /// <summary>
        /// RMD: Remove a directory.
        /// </summary>
        /// <returns>
        /// Null or an error message.
        /// </returns>
        /// <param name='path'>
        /// A relative or absolute path for the directory.
        /// </param>
        string RemoveDirectory(string path);

        /// <summary>
        /// RETR: Open a stream for reading the specified file.
        /// </summary>
        /// <returns>
        /// An opened stream for reading from the file, or null on error.
        /// </returns>
        /// <param name='path'>
        /// A relative or absolute path for the file.
        /// </param>
        Stream ReadFile(string path);

        /// <summary>
        /// STOR: Open a stream for writing to the specified file.
        /// If the file exists, it should be overwritten.
        /// </summary>
        /// <returns>
        /// An opened stream for writing to the file, or null on error.
        /// </returns>
        /// <param name='path'>
        /// A relative or absolute path for the file.
        /// </param>
        Stream WriteFile(string path);

        /// <summary>
        /// DELE: Deletes a file.
        /// </summary>
        /// <returns>
        /// Null or an error message.
        /// </returns>
        /// <param name='path'>
        /// A relative or absolute path for the file.
        /// </param>
        string RemoveFile(string path);

        /// <summary>
        /// LIST: Return a list of files and folders in the current directory, or the optionally specified path.
        /// </summary>
        /// <param name='path'>
        /// An array of file system entries.
        /// </param>
        FileSystemEntry[] ListEntries(string path = null);

        /// <summary>
        /// SIZE: Gets the size of a file in bytes.
        /// </summary>
        /// <returns>
        /// The file size, or -1 on error.
        /// </returns>
        /// <param name='path'>
        /// A relative or absolute path.
        /// </param>
        long GetFileSize(string path);

        /// <summary>
        /// MDTM: Gets the last modified timestamp of a file.
        /// </summary>
        /// <returns>
        /// The last modified time, or null on error.
        /// </returns>
        /// <param name='path'>
        /// A relative or absolute path.
        /// </param>
        DateTime? GetLastModifiedTime(string path);
    }
}

