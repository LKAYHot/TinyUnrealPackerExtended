using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface IFileSystemService
    {

        Task<FolderItem> GetTreeAsync(string rootPath, CancellationToken ct);
        Task CopyAsync(string sourcePath, string destinationPath, bool recursive, CancellationToken ct);
        Task MoveAsync(string sourcePath, string destinationPath, CancellationToken ct);
        Task DeleteAsync(string path, bool recursive, CancellationToken ct);
        Task CreateDirectoryAsync(string path, CancellationToken ct);
        bool DirectoryExists(string path);
        bool FileExists(string path);
    }
}
