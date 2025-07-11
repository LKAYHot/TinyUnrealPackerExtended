using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.Services.FolderEditorVisualServices
{
    public class ItemDoubleClickService
    {
        private readonly Dictionary<string, Func<FolderItem, CancellationToken, Task>> _handlers;

        public ItemDoubleClickService()
        {
            _handlers = new Dictionary<string, Func<FolderItem, CancellationToken, Task>>(StringComparer.OrdinalIgnoreCase);
        }

      
        public void RegisterHandler(string extension, Func<FolderItem, CancellationToken, Task> handler)
        {
            var ext = extension.TrimStart('.').ToLowerInvariant();
            _handlers[ext] = handler;
        }

        
        public async Task HandleAsync(
            FolderItem item,
            CancellationToken ct,
            Action<string, bool> navigate,
            Dispatcher dispatcher,
            Action<FolderItem> openDefault)
        {
            if (item == null)
                return;

            if (item.IsDirectory)
            {
                navigate(item.FullPath, true);
                dispatcher.BeginInvoke(new Action(() => { /* здесь можно сбросить _suppressTreeNav */ }), DispatcherPriority.Background);
                return;
            }

            var ext = Path.GetExtension(item.FullPath)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;

            if (_handlers.TryGetValue(ext, out var handler))
            {
                await handler(item, ct);
            }
            else
            {
                openDefault(item);
            }
        }
    }
}
