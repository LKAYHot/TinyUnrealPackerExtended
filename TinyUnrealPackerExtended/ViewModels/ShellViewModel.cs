using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using TinyUnrealPackerExtended.ViewModels.Pages;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class ShellViewModel : ObservableObject
    {
        public ShellViewModel()
        {
            var themes = new ObservableCollection<string> { "Light", "Dark" };
            var current = Properties.Settings.Default.AppTheme;
            SelectedTheme = current;
            AvailableThemes = themes;

            ShowThemesInternal();
        }

        public ObservableCollection<string> AvailableThemes { get; }
        [ObservableProperty] private string selectedTheme;

        [ObservableProperty] private object currentViewModel;

        partial void OnCurrentViewModelChanged(object oldVm, object newVm)
        {
            AttachVm(newVm);
            SaveSettingsCommand.NotifyCanExecuteChanged();
        }

        private void AttachVm(object vm)
        {
            if (vm is ISettingsViewModel svm)
            {
                svm.PropertyChanged += (_, __) =>
                {
                    SaveSettingsCommand.NotifyCanExecuteChanged();
                };
            }
        }

        [RelayCommand]
        private void ShowThemes()
        {
            ShowThemesInternal();
        }

        private void ShowThemesInternal()
        {
            var vm = new ThemesViewModel(AvailableThemes, SelectedTheme);
            CurrentViewModel = vm;
        }

        [RelayCommand(CanExecute = nameof(CanSaveSettings))]
        private void SaveSettings()
        {
            if (CurrentViewModel is ISettingsViewModel svm)
                svm.Save();

            var exePath = Process.GetCurrentProcess().MainModule.FileName;

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }

        private bool CanSaveSettings()
            => CurrentViewModel is ISettingsViewModel svm && svm.HasChanges;
    }
}
