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
            if (String.IsNullOrWhiteSpace(path))
                return "Empty path specified.";

            string newPath;
            try {
                newPath = Path.GetFullPath(Path.Combine(currentDir.FullName, path));
            } catch (ArgumentException ex) {
                return ex.Message;
            }

            DirectoryInfo newDir = new DirectoryInfo(newPath);
            if (!newDir.Exists)
                return "Path does not exist.";

            currentDir = newDir;
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
    }
}

