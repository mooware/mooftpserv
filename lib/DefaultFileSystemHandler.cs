using System;
using System.Collections.Generic;
using System.IO;

namespace mooftpserv
{
    public class DefaultFileSystemHandler : IFileSystemHandler
    {
        enum OS { WinNT, WinCE, Unix };

        private string currentPath;
        private OS os;

        public DefaultFileSystemHandler(DirectoryInfo startDir)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
              os = OS.WinNT;
            }
            else if (Environment.OSVersion.Platform == PlatformID.WinCE)
            {
              os = OS.WinCE;
            }
            else // probably UNIX
            {
              os = OS.Unix;
            }

            this.currentPath = EncodePath(startDir.FullName);
        }

        private DefaultFileSystemHandler(string path, OS os)
        {
          this.currentPath = path;
          this.os = os;
        }

        public IFileSystemHandler Clone()
        {
            return new DefaultFileSystemHandler(currentPath, os);
        }

        public ResultOrError<string> GetCurrentDirectory()
        {
            return ResultOrError<string>.MakeResult(currentPath);
        }

        public ResultOrError<string> ChangeDirectory(string path)
        {
            string newPath = ResolvePath(path);

#if !WindowsCE
            // special fake root for WinNT drives
            if (os == OS.WinNT && newPath == "/") {
                currentPath = newPath;
                return ResultOrError<string>.MakeResult(newPath);
            }
#endif

            string realPath = DecodePath(newPath);
            if (!Directory.Exists(realPath))
                return ResultOrError<string>.MakeError("Path does not exist.");

            currentPath = newPath;
            return ResultOrError<string>.MakeResult(newPath);
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
                    return ResultOrError<string>.MakeError("Directory already exists.");

                newDir.Create();
            } catch (Exception ex) {
                return ResultOrError<string>.MakeError(ex.Message);
            }

            return ResultOrError<string>.MakeResult(newPath);
        }

        public ResultOrError<bool> RemoveDirectory(string path)
        {
            string newPath = ResolvePath(path);

            try {
                DirectoryInfo newDir = new DirectoryInfo(DecodePath(newPath));
                if (!newDir.Exists)
                    return ResultOrError<bool>.MakeError("Directory does not exist.");

                if (newDir.GetFileSystemInfos().Length > 0)
                    return ResultOrError<bool>.MakeError("Directory is not empty.");

                newDir.Delete();
            } catch (Exception ex) {
                return ResultOrError<bool>.MakeError(ex.Message);
            }

            return ResultOrError<bool>.MakeResult(true);
        }

        public ResultOrError<Stream> ReadFile(string path)
        {
            string newPath = ResolvePath(path);
            string realPath = DecodePath(newPath);

            if (!File.Exists(realPath))
                return ResultOrError<Stream>.MakeError("File does not exist.");

            try {
                return ResultOrError<Stream>.MakeResult(File.OpenRead(realPath));
            } catch (Exception ex) {
                return ResultOrError<Stream>.MakeError(ex.Message);
            }
        }

        public ResultOrError<Stream> WriteFile(string path)
        {
            string newPath = ResolvePath(path);
            string realPath = DecodePath(newPath);

            try {
                return ResultOrError<Stream>.MakeResult(File.Open(realPath, FileMode.OpenOrCreate));
            } catch (Exception ex) {
                return ResultOrError<Stream>.MakeError(ex.Message);
            }
        }

        public ResultOrError<bool> RemoveFile(string path)
        {
            string newPath = ResolvePath(path);
            string realPath = DecodePath(newPath);

            if (!File.Exists(realPath))
                return ResultOrError<bool>.MakeError("File does not exist.");

            try {
                File.Delete(realPath);
            } catch (Exception ex) {
                return ResultOrError<bool>.MakeError(ex.Message);
            }

            return ResultOrError<bool>.MakeResult(true);
        }

        public ResultOrError<bool> RenameFile(string fromPath, string toPath)
        {
            string realFromPath = DecodePath(ResolvePath(fromPath));
            string realToPath = DecodePath(ResolvePath(toPath));

            if (!File.Exists(realFromPath) && !Directory.Exists(realFromPath))
                return ResultOrError<bool>.MakeError("Source path does not exist.");

            if (File.Exists(realToPath) || Directory.Exists(realToPath))
                return ResultOrError<bool>.MakeError("Target path already exists.");

            try {
                File.Move(realFromPath, realToPath);
            } catch (Exception ex) {
                return ResultOrError<bool>.MakeError(ex.Message);
            }

            return ResultOrError<bool>.MakeResult(true);
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

                return ResultOrError<FileSystemEntry[]>.MakeResult(result.ToArray());
            }
#endif

            string realPath = DecodePath(newPath);
            FileSystemInfo[] files;

            if (File.Exists(realPath))
                files = new FileSystemInfo[] { new FileInfo(realPath) };
            else if (Directory.Exists(realPath))
                files = new DirectoryInfo(realPath).GetFileSystemInfos();
            else
                return ResultOrError<FileSystemEntry[]>.MakeError("Path does not exist.");

            foreach (FileSystemInfo file in files) {
                FileSystemEntry entry = new FileSystemEntry();
                entry.Name = file.Name;
                // CF is missing FlagsAttribute.HasFlag
                entry.IsDirectory = ((file.Attributes & FileAttributes.Directory) == FileAttributes.Directory);
                entry.Size = (entry.IsDirectory ? 0 : ((FileInfo) file).Length);
                entry.LastModifiedTimeUtc = file.LastWriteTime.ToUniversalTime();
                result.Add(entry);
            }

            return ResultOrError<FileSystemEntry[]>.MakeResult(result.ToArray());
        }

        public ResultOrError<long> GetFileSize(string path)
        {
            string realPath = DecodePath(ResolvePath(path));
            if (!File.Exists(realPath))
                return ResultOrError<long>.MakeError("File does not exist.");

            long size = new FileInfo(realPath).Length;
            return ResultOrError<long>.MakeResult(size);
        }

        public ResultOrError<DateTime> GetLastModifiedTimeUtc(string path)
        {
            string realPath = DecodePath(ResolvePath(path));
            if (!File.Exists(realPath))
                return ResultOrError<DateTime>.MakeError("File does not exist.");

            // CF is missing FileInfo.LastWriteTimeUtc
            DateTime time = new FileInfo(realPath).LastWriteTime.ToUniversalTime();
            return ResultOrError<DateTime>.MakeResult(time);
        }

        private string ResolvePath(string path)
        {
            // CF is missing String.IsNullOrWhiteSpace
            if (path == null || path.Trim() == "")
                return null;

            // first, make a complete unix path
            string fullPath;
            if (path[0] == '/') {
                fullPath = path;
            } else {
                fullPath = currentPath;
                if (!fullPath.EndsWith("/"))
                    fullPath += "/";
                fullPath += path;
            }

            // then, remove ".." and "."
            List<string> tokens = new List<string>(fullPath.Split('/'));
            for (int i = 0; i < tokens.Count; ++i) {
                if (tokens[i] == "") {
                    if (i == 0 || i == tokens.Count - 1) {
                        continue; // ignore, start and end should be empty tokens
                    } else {
                        tokens.RemoveAt(i);
                        --i;
                    }
                } else if (tokens[i] == "..") {
                    if (i < 2) {
                        // cannot go higher than root, just remove the token
                        tokens.RemoveAt(i);
                        --i;
                    } else {
                        tokens.RemoveRange(i - 1, 2);
                        i -= 2;
                    }
                } else if (i < tokens.Count - 1 && tokens[i].EndsWith(@"\")) {
                    int slashes = 0;
                    for (int c = tokens[i].Length - 1; c >= 0 && tokens[i][c] == '\\'; --c)
                        ++slashes;

                    if (slashes % 2 != 0) {
                        // the slash was actually escaped, merge tokens
                        tokens[i] += ("/" + tokens[i + 1]);
                        ++i;
                    }
                }
            }

            if (tokens.Count > 1)
                return String.Join("/", tokens.ToArray());
            else
                return "/";
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
    }
}
