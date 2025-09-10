using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface IWindowActions
    {
        void Minimize();
        void ToggleMaximizeRestore();
        void Close();
    }
}
