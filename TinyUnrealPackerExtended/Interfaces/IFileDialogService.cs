using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface IFileDialogService
    {
        Task<string?> PickFileAsync(string filter, string title = "Select File", string? initialDirectory = null);
        Task<string[]?> PickFilesAsync(string filter, string title = "Select Files", string? initialDirectory = null);
        Task<string?> SaveFileAsync(string filter, string title = "Save File", string? defaultFileName = null, string? initialDirectory = null);
        Task<string?> PickFolderAsync(string description = "Select Folder", string? initialDirectory = null);
    }
}
