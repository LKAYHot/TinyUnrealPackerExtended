using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.ViewModels;
using System.IO;

namespace TinyUnrealPackerExtended.Services.FolderEditorVisualServices
{
    public class DragDropManager
    {
        private readonly IFileSystemService _fileSystem;
        private readonly Action<string> _showError;
        private readonly Func<FolderItem, ObservableCollection<FolderItem>> _findParentCollection;
        private readonly Action<FolderItem, ObservableCollection<FolderItem>, int> _reparentVisualAt;
        private readonly Action<FolderItem, string> _addFileIntoFolder;

        private FolderItem _draggedItem;
        private FolderItem _lastTarget;
        private Point _dragStartPoint;

        public DragDropManager(
            IFileSystemService fileSystem,
            Action<string> showError,
            Func<FolderItem, ObservableCollection<FolderItem>> findParentCollection,
            Action<FolderItem, ObservableCollection<FolderItem>, int> reparentVisualAt,
            Action<FolderItem, string> addFileIntoFolder)
        {
            _fileSystem = fileSystem;
            _showError = showError;
            _findParentCollection = findParentCollection;
            _reparentVisualAt = reparentVisualAt;
            _addFileIntoFolder = addFileIntoFolder;
        }

        /// <summary>
        /// Вызывается при начале Drag (MouseDown).
        /// </summary>
        public void BeginDrag(MouseButtonEventArgs args)
        {
            _dragStartPoint = args.GetPosition(null);
            if (args.OriginalSource is FrameworkElement fe && fe.DataContext is FolderItem fi)
            {
                _draggedItem = fi;
                _lastTarget = null;
            }
        }

        /// <summary>
        /// Отслеживание перемещения мыши для запуска DragDrop.
        /// </summary>
        public void OnMouseMove(MouseEventArgs args)
        {
            if (_draggedItem == null || args.LeftButton != MouseButtonState.Pressed)
                return;

            var current = args.GetPosition(null);
            if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var data = new DataObject("FolderItem", _draggedItem);
            DragDrop.DoDragDrop(Application.Current.MainWindow, data, DragDropEffects.Move);

            // Сброс состояния
            _draggedItem = null;
            _lastTarget = null;
        }

        /// <summary>
        /// Обработка DragOver: визуальная подсветка и предварительный ReparentVisual.
        /// </summary>
        public void OnDragOver(DragEventArgs args)
        {
            if (args.Data.GetDataPresent("FolderItem"))
                args.Effects = DragDropEffects.Move;
            else if (args.Data.GetDataPresent(DataFormats.FileDrop))
                args.Effects = DragDropEffects.Copy;
            else
                args.Effects = DragDropEffects.None;

            args.Handled = true;

            if (_draggedItem == null)
                return;

            if (!(args.OriginalSource is DependencyObject src))
                return;

            var container = VisualTreeHelperExtensions.GetAncestor<TreeViewItem>(src);
            if (container?.DataContext is not FolderItem target || target == _draggedItem || target == _lastTarget)
                return;

            // Куда вставить визуально
            var dest = target.IsDirectory
                ? target.Children
                : _findParentCollection(target);
            if (dest == null)
                return;

            int insertIndex = dest.Count;
            _reparentVisualAt(_draggedItem, dest, insertIndex);
            _lastTarget = target;
        }

        /// <summary>
        /// Обработка Drop: окончательное перемещение или копирование файлов.
        /// </summary>
        public async Task OnDropAsync(DragEventArgs args)
        {
            // Перемещение своих элементов
            if (args.Data.GetDataPresent("FolderItem") &&
                args.OriginalSource is DependencyObject src &&
                VisualTreeHelperExtensions.GetAncestor<TreeViewItem>(src)?.DataContext is FolderItem target)
            {
                var source = args.Data.GetData("FolderItem") as FolderItem;
                if (source != null)
                {
                    var oldPath = source.FullPath;
                    var newPath = Path.Combine(target.FullPath, source.Name);
                    try
                    {
                        await _fileSystem.MoveAsync(oldPath, newPath, CancellationToken.None);
                        source.FullPath = newPath;
                        // TODO: обновить children для директорий, если нужно
                    }
                    catch (Exception ex)
                    {
                        _showError($"Не удалось переместить элемент: {ex.Message}");
                    }

                    args.Handled = true;
                    _draggedItem = null;
                    _lastTarget = null;
                    return;
                }
            }

            // Копирование файлов из внешнего источника
            if (args.Data.GetDataPresent(DataFormats.FileDrop) &&
                args.OriginalSource is DependencyObject src2 &&
                VisualTreeHelperExtensions.GetAncestor<TreeViewItem>(src2)?.DataContext is FolderItem targetExt)
            {
                var files = (string[])args.Data.GetData(DataFormats.FileDrop);
                try
                {
                    foreach (var srcPath in files)
                    {
                        var fileName = Path.GetFileName(srcPath);
                        var destPath = Path.Combine(targetExt.FullPath, fileName);
                        bool isDir = _fileSystem.DirectoryExists(srcPath);
                        await _fileSystem.CopyAsync(srcPath, destPath, recursive: isDir, ct: CancellationToken.None);
                        _addFileIntoFolder(targetExt, destPath);
                    }
                }
                catch (Exception ex)
                {
                    _showError($"Ошибка при вставке файлов: {ex.Message}");
                }

                args.Handled = true;
            }
        }

        public void FileDropOver(DragEventArgs args)
        {
            args.Effects = args.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            args.Handled = true;
        }

        public async Task FileDropAsync(DragEventArgs args, FolderItem target)
        {
            if (!args.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var paths = (string[])args.Data.GetData(DataFormats.FileDrop);
            if (target == null || !target.IsDirectory)
                return;

            try
            {
                foreach (var srcPath in paths)
                {
                    var fileName = Path.GetFileName(srcPath);
                    var destPath = Path.Combine(target.FullPath, fileName);
                    bool isDir = _fileSystem.DirectoryExists(srcPath);
                    await _fileSystem.CopyAsync(srcPath, destPath, recursive: isDir, ct: CancellationToken.None);
                    _addFileIntoFolder(target, destPath);
                }
            }
            catch (Exception ex)
            {
                _showError($"Ошибка при добавлении файлов: {ex.Message}");
            }
            finally
            {
                args.Handled = true;
            }
        }
    }
}
