using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.ViewModels.Pages
{
    public partial class ThemesViewModel : ObservableObject, ISettingsViewModel
    {
        private readonly string _originalTheme;
        private bool _hasApplied;

        [ObservableProperty]
        private ObservableCollection<string> availableThemes;

        [ObservableProperty]
        private string selectedTheme;

        public ThemesViewModel(ObservableCollection<string> themes, string current)
        {
            AvailableThemes = themes;
            _originalTheme = current;
            SelectedTheme = current;
            _hasApplied = false;
        }

        [RelayCommand]
        private void SelectTheme(string theme)
        {
            SelectedTheme = theme;
        }

        public bool HasChanges => _hasApplied;

        partial void OnSelectedThemeChanged(string oldValue, string newValue)
        {
            _hasApplied = false;
            OnPropertyChanged(nameof(HasChanges));
            ApplyThemeCommand.NotifyCanExecuteChanged();
        }

        public void Save()
        {
            Properties.Settings.Default.AppTheme = SelectedTheme;
            Properties.Settings.Default.Save();
        }

        [RelayCommand(CanExecute = nameof(CanApplyTheme))]
        private void ApplyTheme()
        {
            Save();
            _hasApplied = true;
            OnPropertyChanged(nameof(HasChanges));
            ApplyThemeCommand.NotifyCanExecuteChanged();
        }

        private bool CanApplyTheme()
            => !_hasApplied && SelectedTheme != _originalTheme;
    }

    public interface ISettingsViewModel : INotifyPropertyChanged
    {
        bool HasChanges { get; }
        void Save();
    }
}
