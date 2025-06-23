using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HandyControl.Controls;
using MahApps.Metro.IconPacks;
using TinyUnrealPackerExtended.Services;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly MainWindowViewModel mainWindowViewModel;

        private FolderItem _draggedFolderItem;
        private FolderItem _lastTargetFolderItem;
        private Point _dragStartPoint;
        private TreeView _tree;

        public MainWindow()
        {
            InitializeComponent();
            var growlService = new GrowlService(GrowlContainer);
            var fileDialog = new FileDialogService();
            var proccessRunner = new ProcessRunner();
            var fileSystem = new FileSystemService();
            mainWindowViewModel = new MainWindowViewModel(new DialogService(), growlService, fileDialog, proccessRunner, fileSystem);
            DataContext = mainWindowViewModel;
            _tree = FolderTree;
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;
            e.Handled = true;
        }

        private void LocresZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0) return;
            if (mainWindowViewModel.LocresFiles.Count > 0) return;

            foreach (var path in files)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".csv" || ext == ".locres")
                {
                    if (ext == ".csv")
                    {
                        mainWindowViewModel.IsCsvFileDropped = true;
                        mainWindowViewModel.OriginalLocresFiles.Clear();
                    }
                    else
                    {
                        mainWindowViewModel.IsCsvFileDropped = false;
                    }

                    mainWindowViewModel.LocresFiles.Add(new FileItem
                    {
                        FileName = Path.GetFileName(path),
                        FilePath = path
                    });
                }
            }
        }

        private void OriginalLocresZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0) return;
            if (mainWindowViewModel.OriginalLocresFiles.Count > 0) return;

            foreach (var path in files)
            {
                if (Path.GetExtension(path).ToLowerInvariant() == ".locres")
                {
                    mainWindowViewModel.OriginalLocresFiles.Add(new FileItem
                    {
                        FileName = Path.GetFileName(path),
                        FilePath = path
                    });
                    // Скрываем зону сразу после дропа
                    mainWindowViewModel.IsCsvFileDropped = false;
                }
            }
        }

        private void ExcelZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0) return;
            if (mainWindowViewModel.ExcelFiles.Count > 0) return;

            foreach (var path in files)
            {
                var fileName = Path.GetFileName(path);
                if (!mainWindowViewModel.ExcelFiles.Any(f => f.FileName == fileName))
                {
                    mainWindowViewModel.ExcelFiles.Add(new FileItem
                    {
                        FileName = fileName,
                        FilePath = path
                    });
                }
            }
        }

        private void PakZone_Drop(object sender, DragEventArgs e)
        {
            // проверяем, что у нас есть файл/папка
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            // берём первую папку из списка
            var folder = paths.FirstOrDefault(Directory.Exists);
            if (folder == null) return;

            // очищаем старый выбор и добавляем новое
            mainWindowViewModel.PakFiles.Clear();
            mainWindowViewModel.PakFiles.Add(new FileItem
            {
                FileName = Path.GetFileName(folder), // только имя конечной папки
                FilePath = folder,
                IconKind = PackIconMaterialKind.FolderOutline
            });
        }

        private void UassetZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            var asset = paths.FirstOrDefault(f =>
                Path.GetExtension(f).Equals(".uasset", StringComparison.OrdinalIgnoreCase));
            if (asset == null) return;

            mainWindowViewModel.InjectFiles.Clear();
            mainWindowViewModel.InjectFiles.Add(new FileItem
            {
                FileName = Path.GetFileName(asset),
                FilePath = asset
            });
        }

        private void TextureZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            mainWindowViewModel.TextureFiles.Clear();
            foreach (var p in paths)
                mainWindowViewModel.TextureFiles.Add(new FileItem
                {
                    FileName = Path.GetFileName(p),
                    FilePath = p,
                    IconKind = PackIconMaterialKind.ImageOutline
                });
        }

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

            // 2) Только если тянем наш FolderItem
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
                dest = FindParentCollection(target, mainWindowViewModel.FolderItems);
                if (dest == null) return;
            }

            // 5) Решаем над или под target вставлять
            var pos = e.GetPosition(container);
            int idxTarget = dest.IndexOf(target);
            int insertIndex = pos.Y < container.ActualHeight / 2
                ? idxTarget
                : idxTarget + 1;

            // 6) Перемещаем через ViewModel
            mainWindowViewModel.ReparentVisualAt(_draggedFolderItem, dest, insertIndex);

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

        private void FolderItem_Drop(object sender, DragEventArgs e)
        {
            // Сначала проверяем внутренний Drag&Drop элементов дерева
            if (e.Data.GetDataPresent("FolderItem"))
            {
                var source = e.Data.GetData("FolderItem") as FolderItem;
                if (source != null && sender is TreeViewItem tviInternal && tviInternal.DataContext is FolderItem targetInternal)
                {
                    // Перемещаем узел во ViewModel
                    mainWindowViewModel.MoveFolderItem(source, targetInternal);
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
                    mainWindowViewModel.AddFileIntoFolder(targetExternal, destPath);
                }

                e.Handled = true;  // предотвращаем дальнейшую передачу события
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

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FolderItem fi && fi.IsDirectory)
            {
                // обновляем текущий путь для хлебных крошек
                mainWindowViewModel.FolderEditorRootPath = fi.FullPath;
                mainWindowViewModel.UpdateBreadcrumbs();
            }
        }

        private void OnAnimatedDropZoneLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DashedBorder dz) return;
            if (dz.Background is SolidColorBrush bg && bg.IsFrozen)
                dz.Background = bg.Clone();
            if (dz.BorderBrush is SolidColorBrush bd && bd.IsFrozen)
                dz.BorderBrush = bd.Clone();
        }

        private void OnAnimatedDropZoneDragEnter(object sender, DragEventArgs e)
        {
            if (sender is not DashedBorder dz) return;
            var bg = dz.Background as SolidColorBrush;
            var bd = dz.BorderBrush as SolidColorBrush;
            bg?.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation((Color)ColorConverter.ConvertFromString("#eff8ff"), TimeSpan.FromSeconds(0.2)));
            bd?.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation((Color)ColorConverter.ConvertFromString("#0f80d6"), TimeSpan.FromSeconds(0.2)));
        }

        private void OnAnimatedDropZoneDragLeave(object sender, DragEventArgs e)
        {
            if (sender is not DashedBorder dz) return;
            var bg = dz.Background as SolidColorBrush;
            var bd = dz.BorderBrush as SolidColorBrush;
            bg?.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Colors.White, TimeSpan.FromSeconds(0.2)));
            bd?.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation((Color)ColorConverter.ConvertFromString("#CCCCCC"), TimeSpan.FromSeconds(0.2)));
        }

        private void OnAnimatedDropZoneDrop(object sender, DragEventArgs e)
        {
            if (sender is not DashedBorder dz) return;
            if (dz.Background is SolidColorBrush bg0 && bg0.IsFrozen)
                dz.Background = bg0.Clone();
            if (dz.BorderBrush is SolidColorBrush bd0 && bd0.IsFrozen)
                dz.BorderBrush = bd0.Clone();
            var successBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e6ffed"));
            var successBd = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28a745"));
            dz.Background = successBg;
            dz.BorderBrush = successBd;
            var bgAnim = new ColorAnimation(Colors.White, new Duration(TimeSpan.FromSeconds(0.5))) { BeginTime = TimeSpan.FromSeconds(0.5) };
            var bdAnim = new ColorAnimation((Color)ColorConverter.ConvertFromString("#CCCCCC"), new Duration(TimeSpan.FromSeconds(0.5))) { BeginTime = TimeSpan.FromSeconds(0.5) };
            successBg.BeginAnimation(SolidColorBrush.ColorProperty, bgAnim);
            successBd.BeginAnimation(SolidColorBrush.ColorProperty, bdAnim);
        }

        private void DragArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
