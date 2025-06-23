using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.Services
{
    public class FileDialogService : IFileDialogService
    {
        public Task<string?> PickFileAsync(string filter, string title = "Select File", string? initialDirectory = null)
        {
            var dlg = new OpenFileDialog
            {
                Filter = filter,
                Title = title,
                InitialDirectory = initialDirectory ?? string.Empty,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };
            var result = dlg.ShowDialog();
            return Task.FromResult(result == true ? dlg.FileName : null);
        }

        public Task<string[]?> PickFilesAsync(string filter, string title = "Select Files", string? initialDirectory = null)
        {
            var dlg = new OpenFileDialog
            {
                Filter = filter,
                Title = title,
                InitialDirectory = initialDirectory ?? string.Empty,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = true
            };
            var result = dlg.ShowDialog();
            return Task.FromResult(result == true ? dlg.FileNames : null);
        }

        public Task<string?> SaveFileAsync(string filter, string title = "Save File", string? defaultFileName = null, string? initialDirectory = null)
        {
            var dlg = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                InitialDirectory = initialDirectory ?? string.Empty,
                FileName = defaultFileName ?? string.Empty
            };
            var result = dlg.ShowDialog();
            return Task.FromResult(result == true ? dlg.FileName : null);
        }

        public Task<string?> PickFolderAsync(string description = "Select Folder", string? initialDirectory = null)
        {
            // Используем OpenFileDialog в режиме выбора папки
            var dlg = new OpenFileDialog
            {
                Title = description,
                InitialDirectory = initialDirectory ?? string.Empty,
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Select this folder"
            };
            var result = dlg.ShowDialog();
            if (result != true)
                return Task.FromResult<string?>(null);
            var path = Path.GetDirectoryName(dlg.FileName);
            return Task.FromResult(path);
        }
    }
}
