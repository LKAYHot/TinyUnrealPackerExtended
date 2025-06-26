using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class DialogViewModel : ObservableObject
    {
        [ObservableProperty] private string title;
        [ObservableProperty] private string message;
        [ObservableProperty] private DialogType dialogType;
        [ObservableProperty] private string primaryText;
        [ObservableProperty] private string secondaryText;

        [ObservableProperty] private bool isInputDialog;

        [ObservableProperty] private string responseText = "";
        public DialogViewModel(string title,
                                   string message,
                                   DialogType type,
                                   string primary = "OK",
                                   string secondary = "Cancel",
                                   bool isInput = false,
                                   string initialResponse = "")
        {
            Title = title;
            Message = message;
            DialogType = type;
            PrimaryText = primary;
            SecondaryText = secondary;

            IsInputDialog = isInput;
            ResponseText = initialResponse;
        }

        [RelayCommand] public void SecondaryButton()
        {
            Close(false);
        }

        [RelayCommand]
        private void PrimaryButton()
        {
            Close(true);
        }

        [RelayCommand]
        private void OnSecondary() => Close(false);

        // Закрывает окно, передавая результат
        private void Close(bool dialogResult)
        {
            if (Application.Current.Windows
                .OfType<DialogWindow>()
                .FirstOrDefault(w => w.DataContext == this) is Window wnd)
            {
                wnd.DialogResult = dialogResult;
                wnd.Close();
            }
        }
    }
}
