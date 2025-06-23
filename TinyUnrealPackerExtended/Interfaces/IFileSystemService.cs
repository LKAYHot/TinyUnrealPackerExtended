using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface IFileSystemService
    {
        bool Exists(string path);
        void WriteAllText(string path, string content);
        string ReadAllText(string path);
        void CopyFile(string source, string dest, bool overwrite = false);
        void DeleteFile(string path);
        void CreateDirectory(string path);
        void DeleteDirectory(string path, bool recursive);
        IEnumerable<string> GetFiles(string directory);
        IEnumerable<string> GetDirectories(string directory);
    }
}
