using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.Services
{
    public class BreadcrumbService : IBreadcrumbService
    {
        public ObservableCollection<BreadcrumbItem> Items { get; } = new ObservableCollection<BreadcrumbItem>();
        public event Action OnUpdate;

        private string _rootFolder;

        public void Initialize(string rootFolder)
        {
            _rootFolder = rootFolder.TrimEnd(Path.DirectorySeparatorChar);
            Rebuild(_rootFolder);
        }

        public void Update(string currentFolder)
        {
            Rebuild(currentFolder);
        }

        private void Rebuild(string currentFolder)
        {
            Items.Clear();
            if (string.IsNullOrWhiteSpace(_rootFolder))
                return;

            var list = new List<BreadcrumbItem>
            {
                new BreadcrumbItem
                {
                    Name = Path.GetFileName(_rootFolder),
                    FullPath = _rootFolder
                }
            };

            if (!string.Equals(currentFolder, _rootFolder, StringComparison.OrdinalIgnoreCase))
            {
                var relative = currentFolder
                    .Substring(_rootFolder.Length)
                    .Trim(Path.DirectorySeparatorChar);

                var accum = _rootFolder;
                foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    accum = Path.Combine(accum, part);
                    list.Add(new BreadcrumbItem
                    {
                        Name = part,
                        FullPath = accum
                    });
                }
            }

            foreach (var item in list)
                Items.Add(item);

            OnUpdate?.Invoke();
        }
    }
}
