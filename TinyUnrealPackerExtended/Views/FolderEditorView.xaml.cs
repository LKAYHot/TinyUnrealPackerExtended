using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended.Views
{
    public partial class FolderEditorView : UserControl
    {
        private FolderEditorViewModel _viewModel;
        private FolderItem _draggedFolderItem;
        private FolderItem _lastTargetFolderItem;
        private Point _dragStartPoint;
        private double _savedScrollOffset;
        private bool _suppressTreeNav;

        private List<string> _expandedPaths = new();

        public FolderEditorView()
        {
            InitializeComponent();
            _viewModel = DataContext as FolderEditorViewModel;

            this.DataContextChanged += FolderEditorView_DataContextChanged;
            // Assign TreeView reference
            _tree = this.FolderTree;
        }

        private void FolderEditorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _viewModel = e.NewValue as FolderEditorViewModel;
        }

        private TreeView _tree;

        // TreeView drag-over: internal reparent or external copy
        private void FolderItem_DragOver(object sender, DragEventArgs e)
        {
            // 1) Устанавливаем эффект
            if (e.Data.GetDataPresent("FolderItem"))
                e.Effects = DragDropEffects.Move;
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (_draggedFolderItem == null) return;

            // 3) Находим узел под курсором
            var container = GetNearestContainer(e.OriginalSource as DependencyObject);
            if (container?.DataContext is not FolderItem target) return;
            if (target == _draggedFolderItem || target == _lastTargetFolderItem) return;

            // 4) Выбираем коллекцию-назначение
            ObservableCollection<FolderItem> dest;
            if (target.IsDirectory)
            {
                // внутрь папки
                dest = target.Children;
            }
            else
            {
                // рядом с файлом/папкой — берём ту же коллекцию, что и у target
                dest = FindParentCollection(target, _viewModel.FolderItems);
                if (dest == null) return;
            }

            // 5) Решаем над или под target вставлять
            var pos = e.GetPosition(container);
            int idxTarget = dest.IndexOf(target);
            int insertIndex = pos.Y < container.ActualHeight / 2
                ? idxTarget
                : idxTarget + 1;

            // 6) Перемещаем через ViewModel
            _viewModel.ReparentVisualAt(_draggedFolderItem, dest, insertIndex);

            // 7) Запоминаем, чтобы не дергать при каждом движении
            _lastTargetFolderItem = target;
        }

        private TreeViewItem GetNearestContainer(DependencyObject src)
        {
            while (src != null && !(src is TreeViewItem))
                src = VisualTreeHelper.GetParent(src);
            return src as TreeViewItem;
        }

        private ObservableCollection<FolderItem> FindParentCollection(
           FolderItem item, ObservableCollection<FolderItem> nodes)
        {
            if (nodes.Contains(item))
                return nodes;

            foreach (var node in nodes)
            {
                var found = FindParentCollection(item, node.Children);
                if (found != null)
                    return found;
            }
            return null;
        }



        // TreeView drop: internal move or external file drop
        private void FolderItem_Drop(object sender, DragEventArgs e)
        {
            // Сначала проверяем внутренний Drag&Drop элементов дерева
            if (e.Data.GetDataPresent("FolderItem"))
            {
                var source = e.Data.GetData("FolderItem") as FolderItem;
                if (source != null && sender is TreeViewItem tviInternal && tviInternal.DataContext is FolderItem targetInternal)
                {
                    // Перемещаем узел во ViewModel
                    _viewModel.MoveFolderItem(source, targetInternal);
                    _draggedFolderItem = null;
                    _lastTargetFolderItem = null;
                    e.Handled = true;
                    return;
                }
            }

            // Если нет внутренних элементов — обрабатываем внешний файловый Drop
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && sender is TreeViewItem tviExternal && tviExternal.DataContext is FolderItem targetExternal)
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 0)
                    return;

                foreach (var srcPath in files)
                {
                    var destPath = Path.Combine(targetExternal.FullPath, Path.GetFileName(srcPath));
                    File.Copy(srcPath, destPath, overwrite: true);
                    _viewModel.AddFileIntoFolder(targetExternal, destPath);
                }

                e.Handled = true;  // предотвращаем дальнейшую передачу события
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            // Создаём корневую папку
            Directory.CreateDirectory(targetDir);

            // Копируем все файлы
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            // Рекурсивно копируем подпапки
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private void FolderItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            if (sender is TreeViewItem tvi && tvi.DataContext is FolderItem fi)
                _draggedFolderItem = fi;
        }

        private void FolderItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedFolderItem == null)
                return;

            var current = e.GetPosition(null);
            if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            // Начинаем DragDrop для получения Drop-события
            var data = new DataObject("FolderItem", _draggedFolderItem);
            DragDrop.DoDragDrop(_tree, data, DragDropEffects.Move);

            // Сброс после завершения
            _draggedFolderItem = null;
            _lastTargetFolderItem = null;
        }

        private void FolderTree_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // прокручиваем ScrollViewer на величину дельты
            FolderTreeScrollViewer.ScrollToVerticalOffset(
                FolderTreeScrollViewer.VerticalOffset - e.Delta);

            e.Handled = true;
        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_suppressTreeNav) return;

            if (e.NewValue is FolderItem fi && fi.IsDirectory)
            {
                if (_viewModel.NavigateToCommand.CanExecute(fi.FullPath))
                    _viewModel.NavigateToCommand.Execute(fi.FullPath);

                ExpandAndSelectPath(fi.FullPath);
            }
        }

        private void ExpandAndSelectPath(string fullPath)
        {
            var rootItem = _viewModel.FolderItems.FirstOrDefault();
            if (rootItem == null) return;

            // 2) Разбиваем путь на сегменты относительно корня
            var segments = fullPath
                .Substring(rootItem.FullPath.Length)
                .Trim(Path.DirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            // 3) Начинаем с корневого контейнера
            ItemsControl container = FolderTree;
            FolderItem currentItem = rootItem;

            // 4) Раскрываем корень
            var currentContainer = (TreeViewItem)FolderTree.ItemContainerGenerator
                .ContainerFromItem(rootItem);
            if (currentContainer == null) return;
            currentContainer.IsExpanded = true;

            // 5) Идём по сегментам, раскрывая каждый уровень
            foreach (var seg in segments)
            {
                // находим следующий FolderItem в модели
                var nextItem = currentItem.Children
                    .FirstOrDefault(f => f.Name.Equals(seg, StringComparison.OrdinalIgnoreCase));
                if (nextItem == null) break;

                // даём время WPF сгенерировать контейнер
                currentContainer.UpdateLayout();

                // получаем TreeViewItem для этого элемента
                var nextContainer = (TreeViewItem)currentContainer
                    .ItemContainerGenerator
                    .ContainerFromItem(nextItem);
                if (nextContainer == null) break;

                // раскрываем и спускаемся дальше
                nextContainer.IsExpanded = true;
                currentItem = nextItem;
                currentContainer = nextContainer;
            }

            // 6) В конце — выделяем текущий элемент
            currentContainer.IsSelected = true;
            currentContainer.BringIntoView();
            currentContainer.Focus();
        }

        // ListView handlers
        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;
            e.Handled = true;
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            var targetFolder = _viewModel.SelectedFolderItem;
            if (targetFolder == null || !targetFolder.IsDirectory) return;

            foreach (var srcPath in paths)
            {
                var name = Path.GetFileName(srcPath);
                var destPath = Path.Combine(targetFolder.FullPath, name);

                try
                {
                    if (Directory.Exists(srcPath))
                    {
                        CopyDirectory(srcPath, destPath);
                    }
                    else if (File.Exists(srcPath))
                    {
                        File.Copy(srcPath, destPath, overwrite: true);
                    }
                    else
                    {
                        continue;
                    }

                    _viewModel.AddFileIntoFolder(targetFolder, destPath);
                }
                catch (Exception ex)
                {
                }
            }

            e.Handled = true;
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListViewItem lvi) || !(lvi.DataContext is FolderItem item))
                return;

            if (item.IsDirectory)
            {
                _suppressTreeNav = true;

                if (_viewModel.NavigateToCommand.CanExecute(item.FullPath))
                    _viewModel.NavigateToCommand.Execute(item.FullPath);

                ExpandAndSelectPath(item.FullPath);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _suppressTreeNav = false;
                }), DispatcherPriority.Background);
            }
            else if (Path.GetExtension(item.FullPath)
                     .Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.PreviewTextureCommand.Execute(item);
            }
            else
            {
                _viewModel.OpenFolderCommand.Execute(item);
            }
        }

        // Navigation buttons
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _suppressTreeNav = true;

            _viewModel.GoBackCommand.Execute(null);

            var item = _viewModel.SelectedFolderItem;
            if (item != null && item.IsDirectory)
                ExpandAndSelectPath(item.FullPath);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _suppressTreeNav = false;
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            _suppressTreeNav = true;

            _viewModel.GoForwardCommand.Execute(null);

            var item = _viewModel.SelectedFolderItem;
            if (item != null && item.IsDirectory)
                ExpandAndSelectPath(item.FullPath);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _suppressTreeNav = false;
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void SaveTreeState()
        {
            _expandedPaths.Clear();
            CollectExpanded(FolderTree.Items, "");
            _savedScrollOffset = FolderTreeScrollViewer.VerticalOffset;
        }

        private void CollectExpanded(ItemCollection items, string parentPath)
        {
            foreach (var obj in items)
            {
                if (obj is FolderItem fi)
                {
                    var tvi = (TreeViewItem)FolderTree.ItemContainerGenerator.ContainerFromItem(fi)
                              ?? FindContainerRecursive(FolderTree, fi);
                    var full = fi.FullPath;
                    if (tvi != null && tvi.IsExpanded)
                    {
                        _expandedPaths.Add(full);
                        // обходим детей, передавая текущий
                        CollectExpanded(tvi.Items, full);
                    }
                }
            }
        }

        private TreeViewItem FindContainerRecursive(ItemsControl parent, object item)
        {
            foreach (var obj in parent.Items)
            {
                var tvi = (TreeViewItem)parent.ItemContainerGenerator.ContainerFromItem(obj);
                if (tvi == null) continue;
                if (obj == item) return tvi;
                var child = FindContainerRecursive(tvi, item);
                if (child != null) return child;
            }
            return null;
        }

        private void RestoreTreeState()
        {
            foreach (var path in _expandedPaths)
                ExpandToPath(FolderTree.Items, path);
            FolderTreeScrollViewer.ScrollToVerticalOffset(_savedScrollOffset);
        }

        private bool ExpandToPath(ItemCollection items, string targetPath)
        {
            foreach (var obj in items)
            {
                if (obj is FolderItem fi)
                {
                    var tvi = (TreeViewItem)FolderTree.ItemContainerGenerator.ContainerFromItem(fi)
                              ?? FindContainerRecursive(FolderTree, fi);
                    if (fi.FullPath == targetPath)
                    {
                        if (tvi != null)
                            tvi.IsExpanded = true;
                        return true;
                    }
                    // рекурсивно ищем в детях
                    if (tvi != null && ExpandToPath(tvi.Items, targetPath))
                    {
                        tvi.IsExpanded = true;
                        return true;
                    }
                }
            }
            return false;
        }

        // Search
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                PerformSearch();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) => PerformSearch();

        private void PerformSearch()
        {
            var q = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(q)) return;

            // ищем среди корневых FolderItems
            var match = FindMatch(_viewModel.FolderItems, q);
            if (match == null)
            {
                return;
            }

            string targetPath = match.IsDirectory
                ? match.FullPath
                : Path.GetDirectoryName(match.FullPath);

            _suppressTreeNav = true;

            if (_viewModel.NavigateToCommand.CanExecute(targetPath))
                _viewModel.NavigateToCommand.Execute(targetPath);

            ExpandAndSelectPath(match.FullPath);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _suppressTreeNav = false;
            }), DispatcherPriority.Background);
        }

        private FolderItem FindMatch(IEnumerable<FolderItem> nodes, string query)
        {
            foreach (var node in nodes)
            {
                if (node.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return node;
                var child = FindMatch(node.Children, query);
                if (child != null)
                    return child;
            }
            return null;
        }
    }
}
