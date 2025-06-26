using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface IDialogService
    {
        bool ShowDialog(string title,
                        string message,
                        DialogType dialogType,
                        string primaryText = "OK",
                        string secondaryText = "Cancel");

        string? ShowInputDialog(string title,
                                string message,
                                string initialText = "",
                                string primaryText = "OK",
                                string secondaryText = "Cancel");
    }

    public enum DialogType
    {
        Info,
        Error,
        Confirm
    }
}
