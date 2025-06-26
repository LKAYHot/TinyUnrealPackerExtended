using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.Services
{
    public class DialogService : IDialogService
    {


        public bool ShowDialog(string title, string message, DialogType dialogType,
                               string primaryText = "OK", string secondaryText = "Cancel")
        {
            var vm = new DialogViewModel(title, message, dialogType, primaryText, secondaryText);
            var wnd = new DialogWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };
            bool? result = wnd.ShowDialog();
            return result == true;
        }

        public string? ShowInputDialog(string title, string message, string initialText = "",
                                   string primaryText = "OK", string secondaryText = "Cancel")
        {
            var vm = new DialogViewModel(title, message, DialogType.Info,
                                         primaryText, secondaryText,
                                         isInput: true,
                                         initialResponse: initialText);
            var wnd = new DialogWindow { DataContext = vm, Owner = Application.Current.MainWindow };
            bool? result = wnd.ShowDialog();
            return result == true
                ? vm.ResponseText.Trim()
                : null;
        }
    }

}
