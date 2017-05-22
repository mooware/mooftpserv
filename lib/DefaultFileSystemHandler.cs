using System;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace mooftpserv
{
    /// <summary>
    /// Default file system handler. Allows access to the whole file system. Supports drives on Windows.
    /// </summary>
    public class DefaultFileSystemHandler : IFileSystemHandler
    {
        // list of supported operating systems
        private enum OS { WinNT, WinCE, Unix };

        // currently used operating system
        private OS os;
        // current path as TVFS or unix-like
        private string currentPath;

        public DefaultFileSystemHandler(string startPath)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
              os = OS.WinNT;
            else if (Environment.OSVersion.Platform == PlatformID.WinCE)
              os = OS.WinCE;
            else // probably UNIX
              os = OS.Unix;

            this.currentPath = startPath;
        }

        public DefaultFileSystemHandler() : this("/")
        {
        }

        private DefaultFileSystemHandler(string path, OS os)
        {
          this.currentPath = path;
          this.os = os;
        }

        public IFileSystemHandler Clone(IPEndPoint peer)
        {
            return new DefaultFileSystemHandler(currentPath, os);
        }

        public ResultOrError<string> GetCurrentDirectory()
        {
            return MakeResult<string>(currentPath);
        }

        public ResultOrError<string> ChangeDirectory(string path)
        {
            string newPath = ResolvePath(path);

#if !WindowsCE
            // special fake root for WinNT drives
            if (os == OS.WinNT && newPath == "/") {
                currentPath = newPath;
                return MakeResult<string>(newPath);
            }
#endif

            string realPath = DecodePath(newPath);
            if (!Directory.Exists(realPath))
                return MakeError<string>("Path does not exist.");

            currentPath = newPath;
            return MakeResult<string>(newPath);
        }

        public ResultOrError<string> ChangeToParentDirectory()
        {
            return ChangeDirectory("..");
        }

        public ResultOrError<string> CreateDirectory(string path)
        {
            string newPath = ResolvePath(path);

            try {
                DirectoryInfo newDir = new DirectoryInfo(DecodePath(newPath));
                if (newDir.Exists)
                    return MakeError<string>("Directory already exists.");

                newDir.Create();
            } catch (Exception ex) {
                return MakeError<string>(ex.Message);
            }

            return MakeResult<string>(newPath);
        }

        public ResultOrError<bool> RemoveDirectory(string path)
        {
            string newPath = ResolvePath(path);

            try {
                DirectoryInfo newDir = new DirectoryInfo(DecodePath(newPath));
                if (!newDir.Exists)
                    return MakeError<bool>("Directory does not exist.");

                if (newDir.GetFileSystemInfos().Length > 0)
                    return MakeError<bool>("Directory is not empty.");

                newDir.Delete();
            } catch (Exception ex) {
                return MakeError<bool>(ex.Message);
            }

            return MakeResult<bool>(true);
        }

        public ResultOrError<Stream> ReadFile(string path)
        {
            string newPath = ResolvePath(path);
            string realPath = DecodePath(newPath);

            if (!File.Exists(realPath))
                return MakeError<Stream>("File does not exist.");

            try {
                return MakeResult<Stream>(File.OpenRead(realPath));
            } catch (Exception ex) {
                return MakeError<Stream>(ex.Message);
            }
        }

        public ResultOrError<Stream> WriteFile(string path)
        {
            string newPath = ResolvePath(path);
            string realPath = DecodePath(newPath);

            try {
#if WindowsCE
                // our flash filesystem on WinCE has issues
                // when truncating files, so delete before writing.
                if (File.Exists(realPath))
                    File.Delete(realPath);
#endif

                return MakeResult<Stream>(File.Open(realPath, FileMode.OpenOrCreate));
            } catch (Exception ex) {
                return MakeError<Stream>(ex.Message);
            }
        }

        public ResultOrError<bool> RemoveFile(string path)
        {
            string newPath = ResolvePath(path);
            string realPath = DecodePath(newPath);

            if (!File.Exists(realPath))
                return MakeError<bool>("File does not exist.");

            try {
                File.Delete(realPath);
            } catch (Exception ex) {
                return MakeError<bool>(ex.Message);
            }

            return MakeResult<bool>(true);
        }

        public ResultOrError<bool> RenameFile(string fromPath, string toPath)
        {
            string realFromPath = DecodePath(ResolvePath(fromPath));
            string realToPath = DecodePath(ResolvePath(toPath));

            bool isFile = File.Exists(realFromPath);
            if (!isFile && !Directory.Exists(realFromPath))
                return MakeError<bool>("Source path does not exist.");

            if (File.Exists(realToPath) || Directory.Exists(realToPath))
                return MakeError<bool>("Target path already exists.");

            try {
                if (isFile)
                    File.Move(realFromPath, realToPath);
                else
                    Directory.Move(realFromPath, realToPath);
            } catch (Exception ex) {
                return MakeError<bool>(ex.Message);
            }

            return MakeResult<bool>(true);
        }

        public ResultOrError<FileSystemEntry[]> ListEntries(string path)
        {
            string newPath = ResolvePath(path);
            if (newPath == null)
                newPath = currentPath;

            List<FileSystemEntry> result = new List<FileSystemEntry>();

#if !WindowsCE
            // special fake root for WinNT drives
            if (os == OS.WinNT && newPath == "/") {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in drives) {
                    if (!drive.IsReady)
                        continue;

                    FileSystemEntry entry = new FileSystemEntry();
                    entry.Name = drive.Name[0].ToString();
                    entry.IsDirectory = true;
                    entry.Size = drive.TotalSize;
                    entry.LastModifiedTimeUtc = DateTime.MinValue;
                    result.Add(entry);
                }

                return MakeResult<FileSystemEntry[]>(result.ToArray());
            }
#endif

            string realPath = DecodePath(newPath);
            FileSystemInfo[] files;

            if (File.Exists(realPath))
                files = new FileSystemInfo[] { new FileInfo(realPath) };
            else if (Directory.Exists(realPath))
                files = new DirectoryInfo(realPath).GetFileSystemInfos();
            else
                return MakeError<FileSystemEntry[]>("Path does not exist.");

            foreach (FileSystemInfo file in files) {
                FileSystemEntry entry = new FileSystemEntry();
                entry.Name = file.Name;
                // CF is missing FlagsAttribute.HasFlag
                entry.IsDirectory = ((file.Attributes & FileAttributes.Directory) == FileAttributes.Directory);
                entry.Size = (entry.IsDirectory ? 0 : ((FileInfo) file).Length);
                entry.LastModifiedTimeUtc = file.LastWriteTime.ToUniversalTime();
                result.Add(entry);
            }

            return MakeResult<FileSystemEntry[]>(result.ToArray());
        }

        public ResultOrError<long> GetFileSize(string path)
        {
            string realPath = DecodePath(ResolvePath(path));
            if (Directory.Exists(realPath))
                return MakeError<long>("Cannot get size of directory.");
            else if (!File.Exists(realPath))
                return MakeError<long>("File does not exist.");

            long size = new FileInfo(realPath).Length;
            return MakeResult<long>(size);
        }

        public ResultOrError<DateTime> GetLastModifiedTimeUtc(string path)
        {
            string realPath = DecodePath(ResolvePath(path));
            if (!File.Exists(realPath))
                return MakeError<DateTime>("File does not exist.");

            // CF is missing FileInfo.LastWriteTimeUtc
            DateTime time = new FileInfo(realPath).LastWriteTime.ToUniversalTime();
            return MakeResult<DateTime>(time);
        }

        private string ResolvePath(string path)
        {
            return FileSystemHelper.ResolvePath(currentPath, path);
        }

        private string EncodePath(string path)
        {
            if (os == OS.WinNT)
                return "/" + path[0] + (path.Length > 2 ? path.Substring(2).Replace(@"\", "/") : "");
            else if (os == OS.WinCE)
                return path.Replace(@"\", "/");
            else
                return path;
        }

        private string DecodePath(string path)
        {
            if (path == null || path == "" || path[0] != '/')
                return null;

            if (os == OS.WinNT) {
                // some error checking for the drive layer
                if (path == "/")
                    return null; // should have been caught elsewhere

                if (path.Length > 1 && path[1] == '/')
                    return null;

                if (path.Length > 2 && path[2] != '/')
                    return null;

                if (path.Length < 4) // e.g. "/C/"
                    return path[1] + @":\";
                else
                    return path[1] + @":\" + path.Substring(3).Replace("/", @"\");
            } else if (os == OS.WinCE) {
                return path.Replace("/", @"\");
            } else {
                return path;
            }
        }

        /// <summary>
        /// Shortcut for ResultOrError<T>.MakeResult()
        /// </summary>
        private ResultOrError<T> MakeResult<T>(T result)
        {
            return ResultOrError<T>.MakeResult(result);
        }

        /// <summary>
        /// Shortcut for ResultOrError<T>.MakeError()
        /// </summary>
        private ResultOrError<T> MakeError<T>(string error)
        {
            return ResultOrError<T>.MakeError(error);
        }
    }
}
