using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface IBreadcrumbService
    {
        ObservableCollection<BreadcrumbItem> Items { get; }

        event Action OnUpdate;

        void Initialize(string rootFolder);

        void Update(string currentFolder);
    }
}
