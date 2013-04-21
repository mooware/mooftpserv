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

        public Stream ReadFile(string path)
        {
            string newPath = ResolvePath(path);
            if (!File.Exists(newPath))
                return null;

            try {
                return File.OpenRead(newPath);
            } catch (Exception) {
                // fall through
            }

            return null;
        }

        public Stream WriteFile(string path)
        {
            string newPath = ResolvePath(path);

            try {
                return File.Open(newPath, FileMode.OpenOrCreate);
            } catch (Exception) {
                // fall through
            }

            return null;
        }

        public FileSystemEntry[] ListEntries(string path)
        {
            FileSystemInfo[] files = currentDir.GetFileSystemInfos();
            List<FileSystemEntry> result = new List<FileSystemEntry>();

            foreach (FileSystemInfo file in files) {
                FileSystemEntry entry = new FileSystemEntry();
                entry.Name = file.Name;
                entry.IsDirectory = file.Attributes.HasFlag(FileAttributes.Directory);
                entry.Size = (entry.IsDirectory ? 0 : ((FileInfo) file).Length);
                entry.LastModifiedTime = file.LastWriteTime.ToUniversalTime();
                result.Add(entry);
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

