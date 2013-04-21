using System;
using System.Collections.Generic;
using System.IO;

namespace mooftpserv
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

        public ResultOrError<string> GetCurrentDirectory()
        {
            return ResultOrError<string>.MakeResult(currentDir.FullName);
        }

        public ResultOrError<string> ChangeDirectory(string path)
        {
            string newPath = ResolvePath(path);
            DirectoryInfo newDir = new DirectoryInfo(newPath);
            if (!newDir.Exists)
                return ResultOrError<string>.MakeError("Path does not exist.");

            currentDir = newDir;
            return ResultOrError<string>.MakeResult(newPath);
        }

        public ResultOrError<string> ChangeToParentDirectory()
        {
            return ChangeDirectory("..");
        }

        public ResultOrError<string> CreateDirectory(string path)
        {
            string newPath = ResolvePath(path);
            DirectoryInfo newDir = new DirectoryInfo(newPath);
            if (newDir.Exists)
                return ResultOrError<string>.MakeError("Directory already exists.");

            try {
                newDir.Create();
            } catch (Exception ex) {
                return ResultOrError<string>.MakeError(ex.Message);
            }

            return ResultOrError<string>.MakeResult(newPath);
        }

        public ResultOrError<bool> RemoveDirectory(string path)
        {
            string newPath = ResolvePath(path);
            DirectoryInfo newDir = new DirectoryInfo(newPath);
            if (!newDir.Exists)
                return ResultOrError<bool>.MakeError("Directory does not exist.");

            if (newDir.GetFileSystemInfos().Length > 0)
                return ResultOrError<bool>.MakeError("Directory is not empty.");

            try {
                newDir.Delete();
            } catch (Exception ex) {
                return ResultOrError<bool>.MakeError(ex.Message);
            }

            return ResultOrError<bool>.MakeResult(true);
        }

        public ResultOrError<Stream> ReadFile(string path)
        {
            string newPath = ResolvePath(path);
            if (!File.Exists(newPath))
                return ResultOrError<Stream>.MakeError("File does not exist.");

            try {
                return ResultOrError<Stream>.MakeResult(File.OpenRead(newPath));
            } catch (Exception ex) {
                return ResultOrError<Stream>.MakeError(ex.Message);
            }
        }

        public ResultOrError<Stream> WriteFile(string path)
        {
            string newPath = ResolvePath(path);

            try {
                return ResultOrError<Stream>.MakeResult(File.Open(newPath, FileMode.OpenOrCreate));
            } catch (Exception ex) {
                return ResultOrError<Stream>.MakeError(ex.Message);
            }
        }

        public ResultOrError<bool> RemoveFile(string path)
        {
            string newPath = ResolvePath(path);
            if (!File.Exists(newPath))
                return ResultOrError<bool>.MakeError("File does not exist.");

            try {
                File.Delete(newPath);
            } catch (Exception ex) {
                return ResultOrError<bool>.MakeError(ex.Message);
            }

            return ResultOrError<bool>.MakeResult(true);
        }

        public ResultOrError<bool> RenameFile(string fromPath, string toPath)
        {
            string fullFromPath = ResolvePath(fromPath);
            string fullToPath = ResolvePath(toPath);

            if (!File.Exists(fullFromPath) && !Directory.Exists(fullFromPath))
                return ResultOrError<bool>.MakeError("From-Path does not exist.");

            if (File.Exists(fullToPath) || Directory.Exists(fullToPath))
                return ResultOrError<bool>.MakeError("To-Path already exists.");

            try {
                File.Move(fullFromPath, fullToPath);
            } catch (Exception ex) {
                return ResultOrError<bool>.MakeError(ex.Message);
            }

            return ResultOrError<bool>.MakeResult(true);
        }

        public ResultOrError<FileSystemEntry[]> ListEntries(string path)
        {
            FileSystemInfo[] files = currentDir.GetFileSystemInfos();
            List<FileSystemEntry> result = new List<FileSystemEntry>();

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
            string fullpath = ResolvePath(path);
            if (!File.Exists(fullpath))
                return ResultOrError<long>.MakeError("File does not exist.");

            long size = new FileInfo(fullpath).Length;
            return ResultOrError<long>.MakeResult(size);
        }

        public ResultOrError<DateTime> GetLastModifiedTimeUtc(string path)
        {
            string fullpath = ResolvePath(path);
            if (!File.Exists(fullpath))
                return ResultOrError<DateTime>.MakeError("File does not exist.");

            // CF is missing FileInfo.LastWriteTimeUtc
            DateTime time = new FileInfo(fullpath).LastWriteTime.ToUniversalTime();
            return ResultOrError<DateTime>.MakeResult(time);
        }

        private string ResolvePath(string path)
        {
            // CF is missing String.IsNullOrWhiteSpace
            if (path == null || path.Trim() == "")
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

