using MahApps.Metro.IconPacks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.Services
{
    public class FileSystemService : IFileSystemService
    {
        public async Task<FolderItem> GetTreeAsync(string rootPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var info = new DirectoryInfo(rootPath);
            return await BuildTreeItemAsync(info, ct);
        }

        private async Task<FolderItem> BuildTreeItemAsync(DirectoryInfo dir, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var node = new FolderItem(
                name: dir.Name,
                fullPath: dir.FullName,
                isDirectory: true,
                icon: PackIconMaterialKind.FolderOutline
            );

            // Ленивая загрузка: сначала каталоги
            foreach (var d in dir.GetDirectories())
            {
                var child = await BuildTreeItemAsync(d, ct);
                node.Children.Add(child);
            }

            // Затем файлы
            foreach (var f in dir.GetFiles())
            {
                ct.ThrowIfCancellationRequested();
                node.Children.Add(new FolderItem(
                    name: f.Name,
                    fullPath: f.FullName,
                    isDirectory: false,
                    icon: PackIconMaterialKind.FileOutline
                ));
            }

            return node;
        }

        public Task CopyAsync(string sourcePath, string destinationPath, bool recursive, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);

            if (Directory.Exists(sourcePath))
            {
                if (!recursive)
                    throw new InvalidOperationException("Recursive flag must be true for directories.");
                CopyDirectory(sourcePath, destinationPath);
            }
            else if (File.Exists(sourcePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            else
            {
                throw new FileNotFoundException("Source path not found", sourcePath);
            }

            return Task.CompletedTask;
        }

        private void CopyDirectory(string srcDir, string dstDir)
        {
            Directory.CreateDirectory(dstDir);
            foreach (var file in Directory.GetFiles(srcDir))
                File.Copy(file, Path.Combine(dstDir, Path.GetFileName(file)), overwrite: true);
            foreach (var dir in Directory.GetDirectories(srcDir))
                CopyDirectory(dir, Path.Combine(dstDir, Path.GetFileName(dir)));
        }

        public Task MoveAsync(string sourcePath, string destinationPath, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (Directory.Exists(sourcePath) || File.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                throw new FileNotFoundException("Source path not found", sourcePath);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string path, bool recursive, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive);
            else if (File.Exists(path))
                File.Delete(path);
            else
                throw new FileNotFoundException("Path not found", path);

            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string path, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            Directory.CreateDirectory(path);
            return Task.CompletedTask;
        }

        public bool DirectoryExists(string path) => Directory.Exists(path);
        public bool FileExists(string path) => File.Exists(path);
    }
}
