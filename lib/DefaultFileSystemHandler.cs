using System;
using System.Collections.Generic;
using System.IO;

namespace mooftpserv.lib
{
    public class DefaultFileSystemHandler : IFileSystemHandler
    {
        private DirectoryInfo currentDir;

        public DefaultFileSystemHandler(DirectoryInfo startDir)
        {
            this.currentDir = startDir;
        }

        public IFileSystemHandler Clone()
        {
            return new DefaultFileSystemHandler(currentDir);
        }

        public string GetCurrentDirectory()
        {
            return currentDir.FullName;
        }

        public string ChangeCurrentDirectory(string path)
        {
            string newPath = ResolvePath(path);
            DirectoryInfo newDir = new DirectoryInfo(newPath);
            if (!newDir.Exists)
                return "Path does not exist.";

            currentDir = newDir;
            return null;
        }

        public string CreateDirectory(string path)
        {
            string newPath = ResolvePath(path);
            DirectoryInfo newDir = new DirectoryInfo(newPath);
            if (newDir.Exists)
                return "Directory already exists.";

            try {
                newDir.Create();
            } catch (Exception ex) {
                return ex.Message;
            }

            return null;
        }

        public string RemoveDirectory(string path)
        {
            string newPath = ResolvePath(path);
            DirectoryInfo newDir = new DirectoryInfo(newPath);
            if (!newDir.Exists)
                return "Directory does not exist.";

            if (newDir.GetFileSystemInfos().Length > 0)
                return "Directory is not empty.";

            try {
                newDir.Delete();
            } catch (Exception ex) {
                return ex.Message;
            }

            return null;
        }

        public string[] List(string path)
        {
            FileSystemInfo[] entries = currentDir.GetFileSystemInfos();
            List<string> result = new List<string>();

            foreach (FileSystemInfo entry in entries) {
                result.Add(entry.FullName);
            }

            return result.ToArray();
        }

        public long GetFileSize(string path)
        {
            string fullpath = ResolvePath(path);
            if (!File.Exists(fullpath))
                return -1;

            return new FileInfo(fullpath).Length;
        }

        public DateTime? GetLastModifiedTime(string path)
        {
            string fullpath = ResolvePath(path);
            if (!File.Exists(fullpath))
                return null;

            return new FileInfo(fullpath).LastWriteTimeUtc;
        }

        private string ResolvePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                return null;

            try {
                return Path.GetFullPath(Path.Combine(currentDir.FullName, path));
            } catch (ArgumentException) {
                // fall through
            }

            return null;
        }
    }
}

