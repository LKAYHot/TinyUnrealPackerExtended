using System.Windows;
using System.Windows.Controls;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.Views
{
    public partial class FolderEditorView : UserControl
    {
        private FolderEditorViewModel _viewModel;

        public FolderEditorView()
        {
            InitializeComponent();
            _viewModel = DataContext as FolderEditorViewModel;
            this.DataContextChanged += FolderEditorView_DataContextChanged;
        }

        private void FolderEditorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _viewModel = e.NewValue as FolderEditorViewModel;
            if (_viewModel != null) { 
            _viewModel.FolderTreeControl = this.FolderTree;
            }
        }

    }
}
