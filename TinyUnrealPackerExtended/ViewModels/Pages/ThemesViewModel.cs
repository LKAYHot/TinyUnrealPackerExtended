using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace TinyUnrealPackerExtended.ViewModels.Pages
{
    public partial class ThemesViewModel : ObservableObject, ISettingsViewModel
    {
        private string _originalTheme;
        private string _originalLanguage;

        [ObservableProperty] private ObservableCollection<string> availableThemes;
        [ObservableProperty] private string selectedTheme;

        [ObservableProperty] private ObservableCollection<LanguageOption> availableLanguages;
        [ObservableProperty] private LanguageOption selectedLanguage;

        [ObservableProperty] private bool isPendingRestart;

        public ThemesViewModel(ObservableCollection<string> themes, string currentTheme,
                               ObservableCollection<LanguageOption> languages, LanguageOption currentLang)
        {
            AvailableThemes = themes;
            _originalTheme = currentTheme;
            SelectedTheme = currentTheme;

            AvailableLanguages = languages;
            SelectedLanguage = currentLang;
            _originalLanguage = currentLang?.Code ?? "en";

            IsPendingRestart = false;
        }

        [RelayCommand] private void SelectTheme(string theme) => SelectedTheme = theme;
        [RelayCommand] private void SelectLanguage(LanguageOption lang) => SelectedLanguage = lang;

        public bool HasChanges =>
            SelectedTheme != _originalTheme ||
            (SelectedLanguage?.Code ?? "en") != _originalLanguage;

        partial void OnSelectedThemeChanged(string oldValue, string newValue)
        {
            IsPendingRestart = false;
            OnPropertyChanged(nameof(HasChanges));
            ApplyThemeCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedLanguageChanged(LanguageOption oldValue, LanguageOption newValue)
        {
            IsPendingRestart = false;
            OnPropertyChanged(nameof(HasChanges));
            ApplyThemeCommand.NotifyCanExecuteChanged();
        }

        public void Save()
        {
            Properties.Settings.Default.AppTheme = SelectedTheme;
            Properties.Settings.Default.AppLanguage = SelectedLanguage?.Code ?? "en";
            Properties.Settings.Default.Save();
        }

        [RelayCommand(CanExecute = nameof(CanApplyTheme))]
        private void ApplyTheme()
        {
            Save();

            _originalTheme = SelectedTheme;
            _originalLanguage = SelectedLanguage?.Code ?? "en";

            IsPendingRestart = true;

            OnPropertyChanged(nameof(HasChanges));
            ApplyThemeCommand.NotifyCanExecuteChanged();
        }

        private bool CanApplyTheme() => HasChanges;
    }

    public sealed class LanguageOption
    {
        public string Code { get; }  
        public string DisplayName { get; }

        public LanguageOption(string code, string name)
        {
            Code = code;
            DisplayName = name;
        }

        public override string ToString() => DisplayName;
    }

    public interface ISettingsViewModel : INotifyPropertyChanged
    {
        bool HasChanges { get; }
        bool IsPendingRestart { get; }  
        void Save();
    }
}
