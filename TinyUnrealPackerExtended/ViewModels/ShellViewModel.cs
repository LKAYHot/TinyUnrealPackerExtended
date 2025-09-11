using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using TinyUnrealPackerExtended.ViewModels.Pages;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class ShellViewModel : ObservableObject
    {
        public ShellViewModel()
        {
            var themes = new ObservableCollection<string> { "Light", "Dark" };
            AvailableThemes = themes;
            SelectedTheme = Properties.Settings.Default.AppTheme;

            AvailableLanguages = new ObservableCollection<LanguageOption>
            {
                new LanguageOption("en", "English"),
                new LanguageOption("ru", "Русский")
            };
            var savedLang = Properties.Settings.Default.AppLanguage;
            if (string.IsNullOrWhiteSpace(savedLang)) savedLang = "en";
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == savedLang) ?? AvailableLanguages[0];

            ShowThemesInternal();
        }

        public ObservableCollection<string> AvailableThemes { get; }
        [ObservableProperty] private string selectedTheme;

        public ObservableCollection<LanguageOption> AvailableLanguages { get; }
        [ObservableProperty] private LanguageOption selectedLanguage;

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
        private void ShowThemes() => ShowThemesInternal();

        private void ShowThemesInternal()
        {
            var vm = new ThemesViewModel(AvailableThemes, SelectedTheme, AvailableLanguages, SelectedLanguage);
            CurrentViewModel = vm;
        }

        [RelayCommand(CanExecute = nameof(CanSaveSettings))]
        private void SaveSettings()
        {
            if (CurrentViewModel is ISettingsViewModel svm)
                svm.Save();

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }

            Application.Current.Shutdown();
        }

        private bool CanSaveSettings()
            => CurrentViewModel is ISettingsViewModel svm && svm.IsPendingRestart;
    }
}
