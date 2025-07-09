using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TinyUnrealPackerExtended.Helpers;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class CodePreviewViewModel : ObservableObject
    {
        public TextDocument CodeDocument { get; }
        private readonly CodePreviewWindow _window;

        private int _lastSearchOffset = -1;

        [ObservableProperty]
        private string searchQuery;

        partial void OnSearchQueryChanged(string value)
        {
            _lastSearchOffset = -1;
        }

        private readonly FullscreenHelper _fullscreenHelper;



        public CodePreviewViewModel(string json, CodePreviewWindow window)
        {
            CodeDocument = new TextDocument(json);
            _window = window;
            _fullscreenHelper = new FullscreenHelper(window);

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
            int startIndex = _lastSearchOffset >= 0
                ? _lastSearchOffset + SearchQuery.Length
                : 0;

            int nextIndex = text.IndexOf(SearchQuery, startIndex, StringComparison.OrdinalIgnoreCase);
            if (nextIndex < 0)
            {
                nextIndex = text.IndexOf(SearchQuery, 0, StringComparison.OrdinalIgnoreCase);
                if (nextIndex < 0)
                    return; 
            }

            _lastSearchOffset = nextIndex;
            var line = editor.Document.GetLineByOffset(nextIndex).LineNumber;
            editor.ScrollTo(line, 0);
            editor.Select(nextIndex, SearchQuery.Length);
        }



        [RelayCommand]
        private void MaximizeWindow() => _fullscreenHelper.ToggleFullscreen();

        [RelayCommand]
        private void MinimizeWindow() => _fullscreenHelper.ToggleMinimizeScreen();
    }
}
