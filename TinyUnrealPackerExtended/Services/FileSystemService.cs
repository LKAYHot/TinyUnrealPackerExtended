using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.Services
{
    public class FileSystemService : IFileSystemService
    {
        public bool Exists(string path)
            => File.Exists(path) || Directory.Exists(path);

        public void WriteAllText(string path, string content)
            => File.WriteAllText(path, content);

        public string ReadAllText(string path)
            => File.ReadAllText(path);

        public void CopyFile(string source, string dest, bool overwrite = false)
            => File.Copy(source, dest, overwrite);

        public void DeleteFile(string path)
            => File.Delete(path);

        public void CreateDirectory(string path)
            => Directory.CreateDirectory(path);

        public void DeleteDirectory(string path, bool recursive)
            => Directory.Delete(path, recursive);

        public IEnumerable<string> GetFiles(string directory)
            => Directory.GetFiles(directory);

        public IEnumerable<string> GetDirectories(string directory)
            => Directory.GetDirectories(directory);
    }
}
