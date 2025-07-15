using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.AvalonEdit.Document;
using TinyUnrealPackerExtended.Helpers;
using TinyUnrealPackerExtended.Services;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class CodePreviewViewModel : ObservableObject
    {
        public TextDocument CodeDocument { get; }
        public bool IsEditable { get; }

        private readonly string _sourcePath;
        private readonly LocresService _locresService;
        private readonly FullscreenHelper _fullscreenHelper;
        private readonly CodePreviewWindow _window;
        private int _lastSearchOffset = -1;

        private readonly GrowlService _growlService;

        [ObservableProperty]
        private string searchQuery;

        public CodePreviewViewModel(
            string json,
            string sourcePath,
            CodePreviewWindow window,
            LocresService locresService, GrowlService growlService)
        {
            CodeDocument = new TextDocument(json);
            _sourcePath = sourcePath;
            _locresService = locresService;
            _window = window;
            _growlService = growlService;
            _fullscreenHelper = new FullscreenHelper(window);

            IsEditable = Path.GetExtension(sourcePath)
                             .Equals(".locres", StringComparison.OrdinalIgnoreCase);
        }

        partial void OnSearchQueryChanged(string value)
        {
            _lastSearchOffset = -1;
        }

        [RelayCommand]
        private void FocusSearch()
        {
            _window.SearchBox.Focus();
            _window.SearchBox.SelectAll();
        }

        [RelayCommand]
        private void Search()
        {
            if (string.IsNullOrEmpty(SearchQuery))
                return;

            var editor = _window.EditorMain;
            var text = editor.Text;
            var start = _lastSearchOffset >= 0
                ? _lastSearchOffset + SearchQuery.Length
                : 0;

            var next = text.IndexOf(SearchQuery, start, StringComparison.OrdinalIgnoreCase);
            if (next < 0)
            {
                next = text.IndexOf(SearchQuery, 0, StringComparison.OrdinalIgnoreCase);
                if (next < 0) return;
            }

            _lastSearchOffset = next;
            var line = editor.Document.GetLineByOffset(next).LineNumber;
            editor.ScrollTo(line, 0);
            editor.Select(next, SearchQuery.Length);
        }

        [RelayCommand]
        private void MaximizeWindow() => _fullscreenHelper.ToggleFullscreen();

        [RelayCommand]
        private void MinimizeWindow() => _fullscreenHelper.ToggleMinimizeScreen();

        [RelayCommand(CanExecute = nameof(IsEditable))]
        private async Task SaveAsync()
        {
            try
            {
                var currentJson = CodeDocument.Text;
                await Task.Run(() =>
                    _locresService.ImportFromJson(currentJson, _sourcePath, _sourcePath)
                );

                // Вместо MessageBox:
                _growlService.ShowSuccess(
                    "Файл .locres успешно сохранён",
                    title: "Успешно",
                    duration: 3
                );
            }
            catch (Exception ex)
            {
                // И здесь тоже:
                _growlService.ShowError(
                    $"Ошибка при сохранении .locres: {ex.Message}",
                    title: "Ошибка",
                    duration: 5
                );
            }
        }
    }
}
