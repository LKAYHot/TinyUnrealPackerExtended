using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TinyUnrealPackerExtended.ViewModels
{
    
    public partial class FolderEditorViewModel : ViewModelBase
    {
        private readonly IFileDialogService _fileDialog;
        private readonly IDialogService _dialog;
        private readonly IBreadcrumbService _breadcrumbs = new BreadcrumbService();
        private readonly ITexturePreviewService _textureService;


        private readonly Stack<string> _backStack = new();
        private readonly Stack<string> _forwardStack = new();



        [ObservableProperty] private string rootFolder;
        [ObservableProperty] private string folderEditorRootPath;
        [ObservableProperty] private FolderItem selectedFolderItem;
        [ObservableProperty] private bool canGoBack;
        [ObservableProperty] private bool canGoForward;
        [ObservableProperty] private int maxVisible = int.MaxValue;

        public TreeView FolderTreeControl { get; set; }
        private FolderItem _draggedFolderItem;
        private FolderItem _lastTargetFolderItem;
        private Point _dragStartPoint;

        [ObservableProperty] private bool canEditFolderEditor;

        [ObservableProperty] private ImageSource selectedTexturePreview;
        [ObservableProperty] private string previewedUassetPath;

        [ObservableProperty] private string searchQuery;

        public ObservableCollection<FolderItem> FolderItems { get; } = new();
        public ObservableCollection<BreadcrumbItem> Breadcrumbs => _breadcrumbs.Items;
        public List<BreadcrumbItem> Overflow { get; private set; } = new();


        private FolderItem _clipboardItem;
        private bool _isCut;
        public bool CanPaste => _clipboardItem != null;

        private bool _suppressTreeNav;


        [ObservableProperty] private bool isAlphaEnabled = true;
        private BitmapSource _originalTexture;

       
        public IEnumerable<BreadcrumbItem> DisplayBreadcrumbs
        {
            get
            {
                var all = Breadcrumbs.ToList();
                if (all.Count <= MaxVisible)
                    return all;

                // overflow: first, ellipsis, last N
                var overflowList = all.Skip(1).Take(all.Count - MaxVisible + 1).ToList();
                var result = new List<BreadcrumbItem> { all[0] };
                result.Add(new BreadcrumbItem { Name = "…", IsOverflow = true });
                result.AddRange(all.Skip(all.Count - (MaxVisible - 1)));
                Overflow = overflowList;
                return result;
            }
        }

        public FolderEditorViewModel(
            GrowlService growlService,
            IFileDialogService fileDialogService, IDialogService dialogService)
            : base(fileDialogService, growlService)
        {
            _dialog = dialogService;
            _fileDialog = fileDialogService;
            _textureService = new TexturePreviewService();
        }

        [RelayCommand]
        private void LoadFolderEditor()
        {
            if (string.IsNullOrEmpty(FolderEditorRootPath) || !Directory.Exists(FolderEditorRootPath))
                return;

            FolderItems.Clear();
            ClearTexture();

            RootFolder = FolderEditorRootPath;

            var rootItem = BuildTreeItem(new DirectoryInfo(FolderEditorRootPath));
            FolderItems.Add(rootItem);
            SelectedFolderItem = rootItem;

            _breadcrumbs.Initialize(RootFolder);
            _breadcrumbs.OnUpdate += () => OnPropertyChanged(nameof(DisplayBreadcrumbs));
            UpdateNavigationProperties();
        }

        [RelayCommand]
        private void NavigateTo(string path) => DoNavigateInternal(path, false);

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private void GoBack()
        {
            if (_backStack.Count == 0) return;
            var prev = _backStack.Pop();
            _forwardStack.Push(FolderEditorRootPath);

            _suppressTreeNav = true;
            DoNavigateInternal(prev, addToHistory: false);
            Application.Current.Dispatcher.BeginInvoke(new Action(() => _suppressTreeNav = false),
                                                       DispatcherPriority.Background);
        }

        [RelayCommand(CanExecute = nameof(CanGoForward))]
        private void GoForward()
        {
            if (_forwardStack.Count == 0) return;
            var next = _forwardStack.Pop();
            _backStack.Push(FolderEditorRootPath);

            _suppressTreeNav = true;
            DoNavigateInternal(next, addToHistory: false);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _suppressTreeNav = false;
            }), DispatcherPriority.Background);
        }

        [RelayCommand]
        private void RefreshFolder()
        {
            _backStack.Clear();
            _forwardStack.Clear();
            FolderEditorRootPath = RootFolder;
            LoadFolderEditor();
        }

        [RelayCommand]
        private void OpenFolder(FolderItem item)
        {
            if (item == null) return;

            string arg;
            if (item.IsDirectory)
            {
                arg = $"\"{item.FullPath}\"";
            }
            else
            {
                var dir = Path.GetDirectoryName(item.FullPath);
                arg = $"/select,\"{item.FullPath}\"";
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", arg)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _growlService.ShowError(ex.Message);
            }
        }

        [RelayCommand]
        private void CutFolderItem(FolderItem item)
        {

            _clipboardItem = item;
            _isCut = true;
            OnPropertyChanged(nameof(CanPaste));
        }

        [RelayCommand]
        private void CopyFolderItem(FolderItem item)
        {
            _clipboardItem = item;
            _isCut = false;
            OnPropertyChanged(nameof(CanPaste));
        }

        [RelayCommand]
        private void RenameFolderItem(FolderItem item)
        {
            if (item == null)
                return;

            var oldPath = item.FullPath;

            string title = "Переименовать";
            string message = $"Введите новое имя для «{item.Name}»";
            string? newName = _dialog.ShowInputDialog(
                title: title,
                message: message,
                initialText: item.Name,
                primaryText: "Переименовать",
                secondaryText: "Отмена"
            );
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name)
                return;

            var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newName);

            try
            {
                if (item.IsDirectory)
                    Directory.Move(oldPath, newPath);
                else
                    File.Move(oldPath, newPath);

                item.Name = newName;
                item.FullPath = newPath;

                if (item.IsDirectory)
                    UpdateChildrenPaths(item, oldPath, newPath);
            }
            catch (Exception ex)
            {
                _dialog.ShowDialog(
                    title: "Ошибка переименования",
                    message: ex.Message,
                    dialogType: DialogType.Error,
                    primaryText: "ОК",
                    secondaryText: null
                );
            }
        }

        [RelayCommand]
        private void ShowProperties(FolderItem item)
        {
            if (item == null) return;
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FullPath}\"") { UseShellExecute = true });
        }

        private void UpdateChildrenPaths(FolderItem parent, string oldParentPath, string newParentPath)
        {
            foreach (var child in parent.Children)
            {
                // строим новый полный путь
                var newChildPath = Path.Combine(newParentPath, child.Name);
                child.FullPath = newChildPath;

                if (child.IsDirectory)
                    UpdateChildrenPaths(child,
                                        oldParentPath: Path.Combine(oldParentPath, child.Name),
                                        newParentPath: newChildPath);
            }
        }

        [RelayCommand]
        private void PasteIntoFolder(FolderItem target)
        {
            if (_clipboardItem == null || target == null) return;
            var src = _clipboardItem.FullPath;
            var dest = Path.Combine(target.FullPath, _clipboardItem.Name);

            if (_isCut)
                Directory.Move(src, dest);
            else
            {
                if (_clipboardItem.IsDirectory)
                    CopyDirectory(src, dest);
                else
                    File.Copy(src, dest);
            }

            // Добавляем в модель дерева
            var clone = new FolderItem(
                _clipboardItem.Name,
                dest,
                _clipboardItem.IsDirectory,
                _clipboardItem.IconKind
            );
            target.Children.Add(clone);

            if (_isCut)
            {
                // удаляем оригинал
                RemoveFromParent(FolderItems, _clipboardItem);
                _clipboardItem = null;
            }

            OnPropertyChanged(nameof(CanPaste));
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        partial void OnMaxVisibleChanged(int oldValue, int newValue)
        {
            _breadcrumbs.Update(FolderEditorRootPath);
        }

       
        private void DoNavigateInternal(string path, bool addToHistory)
        {
            if (addToHistory && !string.IsNullOrEmpty(FolderEditorRootPath))
                _backStack.Push(FolderEditorRootPath);

            FolderEditorRootPath = path;
            SelectedFolderItem = FindFolderItem(path, FolderItems) ?? SelectedFolderItem;
            _breadcrumbs.Update(path);
            UpdateNavigationProperties();
            ExpandAndSelectPath(path);
        }

        private void UpdateNavigationProperties()
        {
            CanGoBack = _backStack.Count > 0;
            CanGoForward = _forwardStack.Count > 0;

            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }


        private FolderItem BuildTreeItem(DirectoryInfo dir)
        {
            var node = new FolderItem(dir.Name, dir.FullName, true, PackIconMaterialKind.FolderOutline);
            foreach (var d in dir.GetDirectories())
                node.Children.Add(BuildTreeItem(d));
            foreach (var f in dir.GetFiles())
                node.Children.Add(new FolderItem(f.Name, f.FullName, false, PackIconMaterialKind.FileOutline));
            return node;
        }

        private FolderItem FindFolderItem(string path, IEnumerable<FolderItem> source)
        {
            foreach (var node in source)
            {
                if (node.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                    return node;
                var child = FindFolderItem(path, node.Children);
                if (child != null) return child;
            }
            return null;
        }

        // Texture extraction logic
        [RelayCommand]
        private void ClearTexture()
        {
            SelectedTexturePreview = null;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }

        [RelayCommand]
        private void ClearTexturePreview()
        {
            ClearTexture();
        }

        [RelayCommand]
        private async Task PreviewTextureAsync(FolderItem item)
        {
            try
            {
                ClearTexture();
                var bmp = await _textureService.ExtractAsync(item.FullPath);
                _originalTexture = bmp;
                SelectedTexturePreview = _originalTexture; 
                PreviewedUassetPath = item.FullPath;
                IsAlphaEnabled = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        [RelayCommand]
        private async Task SaveTextureAsync()
        {
            if (string.IsNullOrEmpty(PreviewedUassetPath))
                return;

            byte[] data;
            try
            {
                var bitmap = await _textureService
                                    .ExtractFullResolutionAsync(PreviewedUassetPath, CancellationToken.None);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                data = ms.ToArray();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка сохранения текстуры: {ex.Message}");
                return;
            }

            var saveDlg = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                FileName = Path.GetFileNameWithoutExtension(PreviewedUassetPath) + ".png"
            };
            if (saveDlg.ShowDialog() != true)
                return;

            await File.WriteAllBytesAsync(saveDlg.FileName, data);
        }

        [RelayCommand]
        private void ToggleAlphaChannel()
        {
            if (_originalTexture == null)
                return;

            if (IsAlphaEnabled)
            {
                SelectedTexturePreview = MakeOpaque(_originalTexture);
                IsAlphaEnabled = false;
            }
            else
            {
                SelectedTexturePreview = _originalTexture;
                IsAlphaEnabled = true;
            }
        }

        private BitmapSource MakeOpaque(BitmapSource src)
        {
            int w = src.PixelWidth, h = src.PixelHeight;
            int bpp = src.Format.BitsPerPixel;
            int bytesPerPixel = bpp / 8;
            int stride = (w * bpp + 7) / 8;
            var pixels = new byte[h * stride];
            src.CopyPixels(pixels, stride, 0);

            // для каждого пикселя ставим alpha = 255
            for (int i = bytesPerPixel - 1; i < pixels.Length; i += bytesPerPixel)
                pixels[i] = 0xFF;

            var wb = new WriteableBitmap(w, h, src.DpiX, src.DpiY, src.Format, null);
            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
            wb.Freeze();
            return wb;
        }

        [RelayCommand]
        private void RemoveFolderItem(FolderItem item)
        {
            if (item == null) return;

            bool ok = _dialog.ShowDialog(
               title: "Удалить элемент?",
               message: $"Вы точно хотите удалить «{item.Name}»?",
               dialogType: DialogType.Confirm,
               primaryText: "Да",
               secondaryText: "Нет"
            );
            if (!ok) return;

            try
            {
                if (item.IsDirectory)
                    Directory.Delete(item.FullPath, true);
                else
                    File.Delete(item.FullPath);
                RemoveFromParent(FolderItems, item);
            }
            catch (IOException ex)
            {
                _dialog.ShowDialog(
                   title: "Ошибка удаления",
                   message: ex.Message,
                   dialogType: DialogType.Error,
                   primaryText: "ОК",
                   secondaryText: null
                );
            }
        }

        [RelayCommand]
        private void CopyPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;
            Clipboard.SetText(fullPath);
        }

        private bool RemoveFromParent(
        ObservableCollection<FolderItem> collection,
        FolderItem target)
        {
            if (collection.Remove(target))
                return true;

            foreach (var child in collection)
            {
                if (RemoveFromParent(child.Children, target))
                    return true;
            }

            return false;
        }

        public void AddFileIntoFolder(FolderItem parent, string fullPath)
        {
            parent.Children.Add(new FolderItem
            {
                Name = Path.GetFileName(fullPath),
                FullPath = fullPath,
                IsDirectory = false,
                IconKind = PackIconMaterialKind.FileOutline
            });
        }

        [RelayCommand]
        private void AddFolderToFolder()
        {
            if (SelectedFolderItem == null || !SelectedFolderItem.IsDirectory) return;
            var newDir = Path.Combine(SelectedFolderItem.FullPath, "NewFolder");
            Directory.CreateDirectory(newDir);
            SelectedFolderItem.Children.Add(new FolderItem
            {
                Name = Path.GetFileName(newDir),
                FullPath = newDir,
                IsDirectory = true
            });
        }

        public void ReparentVisualAt(
FolderItem source,
ObservableCollection<FolderItem> destCollection,
int insertIndex)
        {
            RemoveFromParent(FolderItems, source);

            if (insertIndex < 0) insertIndex = 0;
            if (insertIndex > destCollection.Count) insertIndex = destCollection.Count;

            destCollection.Insert(insertIndex, source);
        }

        public void ReparentVisual(FolderItem source, FolderItem newParent)
        {
            RemoveFromParent(FolderItems, source);
            // сразу добавляет в конец newParent.Children
            newParent.Children.Add(source);
        }

        public void MoveFolderItem(FolderItem source, FolderItem target)
        {
            if (source == null || target == null || source == target) return;

            try
            {

                var oldPath = source.FullPath;
                var newPath = Path.Combine(target.FullPath, source.Name);

                if (source.IsDirectory)
                    Directory.Move(oldPath, newPath);
                else
                    File.Move(oldPath, newPath);

                RemoveFromParent(FolderItems, source);

                source.FullPath = newPath;

                if (source.IsDirectory)
                    UpdateChildrenPaths(source, oldParentPath: oldPath, newParentPath: newPath);

                target.Children.Add(source);
            }
            catch (Exception ex)
            {
                _growlService.ShowError(ex.Message);
            }
        }

        [RelayCommand]
        private void OpenInExplorer(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;
            // откроет папку и выделит файл
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{fullPath}\"")
            {
                UseShellExecute = true
            });
        }


        [RelayCommand]
        private void BeginDrag(MouseButtonEventArgs args)
        {
            // Запоминаем точку старта и сам элемент
            _dragStartPoint = args.GetPosition(null);
            if (args.OriginalSource is FrameworkElement fe
                && fe.DataContext is FolderItem fi)
            {
                _draggedFolderItem = fi;
                _lastTargetFolderItem = null;
            }
        }

        [RelayCommand]
        private void PreviewMouseDown(MouseButtonEventArgs args)
        {
            // Запоминаем точку старта и элемент для Drag&Drop
            _dragStartPoint = args.GetPosition(null);
            if (args.OriginalSource is FrameworkElement fe && fe.DataContext is FolderItem fi)
            {
                _draggedFolderItem = fi;
            }
        }

        // 2.2. Движение мыши — проверяем, перешли ли Threshold, и запускаем DragDrop
        [RelayCommand]
        private void OnMouseMove(MouseEventArgs args)
        {
            if (_draggedFolderItem == null || args.LeftButton != MouseButtonState.Pressed)
                return;

            var current = args.GetPosition(null);
            if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            // Запускаем нативный DragDrop
            var data = new DataObject("FolderItem", _draggedFolderItem);
            DragDrop.DoDragDrop(Application.Current.MainWindow, data, DragDropEffects.Move);

            // Сброс состояния
            _draggedFolderItem = null;
            _lastTargetFolderItem = null;
        }

        // 2.3. Перетаскивание над элементом — аналог FolderItem_DragOver
        [RelayCommand]
        private void OnDragOver(DragEventArgs args)
        {
            // 1) Устанавливаем эффект
            if (args.Data.GetDataPresent("FolderItem"))
                args.Effects = DragDropEffects.Move;
            else if (args.Data.GetDataPresent(DataFormats.FileDrop))
                args.Effects = DragDropEffects.Copy;
            else
                args.Effects = DragDropEffects.None;
            args.Handled = true;

            if (_draggedFolderItem == null) return;

            // 2) Находим target
            if (!(args.OriginalSource is DependencyObject src)) return;
            var container = VisualTreeHelperExtensions.GetAncestor<TreeViewItem>(src);
            if (container?.DataContext is not FolderItem target) return;
            if (target == _draggedFolderItem || target == _lastTargetFolderItem) return;

            var dest = target.IsDirectory
                       ? target.Children
                       : FindParentCollection(target, FolderItems);

            if (dest == null) return;

            int insertIndex = dest.Count;

            ReparentVisualAt(_draggedFolderItem, dest, insertIndex);

            _lastTargetFolderItem = target;
        }

        // 2.4. Бросок — аналог FolderItem_Drop
        [RelayCommand]
        private void OnDrop(DragEventArgs args)
        {
            if (args.Data.GetDataPresent("FolderItem") &&
                args.OriginalSource is DependencyObject src &&
                VisualTreeHelperExtensions.GetAncestor<TreeViewItem>(src) is TreeViewItem tvi &&
                tvi.DataContext is FolderItem target)
            {
                var source = args.Data.GetData("FolderItem") as FolderItem;
                if (source != null)
                {
                    MoveFolderItem(source, target);
                    _draggedFolderItem = null;
                    _lastTargetFolderItem = null;
                    args.Handled = true;
                    return;
                }
            }

            if (args.Data.GetDataPresent(DataFormats.FileDrop) &&
                args.OriginalSource is DependencyObject src2 &&
                VisualTreeHelperExtensions.GetAncestor<TreeViewItem>(src2)?.DataContext is FolderItem targetExt)
            {
                var files = (string[])args.Data.GetData(DataFormats.FileDrop);
                foreach (var srcPath in files)
                {
                    var destPath = Path.Combine(targetExt.FullPath, Path.GetFileName(srcPath));
                    File.Copy(srcPath, destPath, overwrite: true);
                    AddFileIntoFolder(targetExt, destPath);
                }
                args.Handled = true;
            }
        }

        private ObservableCollection<FolderItem> FindParentCollection(
    FolderItem item,
    ObservableCollection<FolderItem> nodes)
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

        [RelayCommand]
        private void FileDropOver(DragEventArgs args)
        {
            args.Effects = args.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            args.Handled = true;
        }

        [RelayCommand]
        private void FileDrop(DragEventArgs args)
        {
            if (!args.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var paths = (string[])args.Data.GetData(DataFormats.FileDrop);
            if (SelectedFolderItem == null || !SelectedFolderItem.IsDirectory) return;

            foreach (var srcPath in paths)
            {
                CopyDirectoryOrFile(srcPath, SelectedFolderItem);
            }
            args.Handled = true;
        }

        private void CopyDirectoryOrFile(string srcPath, FolderItem target)
        {
            var dest = Path.Combine(target.FullPath, Path.GetFileName(srcPath));
            if (Directory.Exists(srcPath))
                CopyDirectory(srcPath, dest);
            else if (File.Exists(srcPath))
                File.Copy(srcPath, dest, overwrite: true);

            AddFileIntoFolder(target, dest);
        }

        [RelayCommand]
        private void ItemDoubleClick(FolderItem item)
        {
            if (item == null) return;

            if (item.IsDirectory)
            {
                _suppressTreeNav = true;
                DoNavigateInternal(item.FullPath, addToHistory: true);
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _suppressTreeNav = false;
                }), DispatcherPriority.Background);
            }
            else if (Path.GetExtension(item.FullPath)
                             .Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                PreviewTextureCommand.Execute(item);
            }
            else
            {
                OpenFolderCommand.Execute(item);
            }
        }

        partial void OnSelectedFolderItemChanged(FolderItem oldItem, FolderItem newItem)
        {
            if (newItem == null || !newItem.IsDirectory) return;

            _suppressTreeNav = true;
            NavigateToCommand.Execute(newItem.FullPath);
            ExpandAndSelectPath(newItem.FullPath);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _suppressTreeNav = false;
            }), DispatcherPriority.Background);
        }

        public void ExpandAndSelectPath(string fullPath)
        {
            var root = FolderItems.FirstOrDefault();
            if (root == null) return;

            var rootContainer = (TreeViewItem)TreeViewContainerFromItem(root);
            if (rootContainer == null) return;
            rootContainer.IsExpanded = true;

            var segments = fullPath
                .Substring(root.FullPath.Length)
                .Trim(Path.DirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            var currentContainer = rootContainer;
            var currentItem = root;

            foreach (var seg in segments)
            {
                currentContainer.UpdateLayout();
                var nextItem = currentItem.Children
                    .FirstOrDefault(c =>
                        string.Equals(c.Name, seg, StringComparison.OrdinalIgnoreCase));
                if (nextItem == null) break;

                var nextContainer = (TreeViewItem)
                    currentContainer.ItemContainerGenerator
                                    .ContainerFromItem(nextItem);
                if (nextContainer == null) break;

                nextContainer.IsExpanded = true;
                currentItem = nextItem;
                currentContainer = nextContainer;
            }

            currentContainer.IsSelected = true;
            currentContainer.BringIntoView();
            currentContainer.Focus();
        }

        private DependencyObject TreeViewContainerFromItem(object item)
        {
            return FolderTreeControl.ItemContainerGenerator.ContainerFromItem(item);
        }


        [RelayCommand]
        private void TreeSelectionChanged(RoutedPropertyChangedEventArgs<object> args)
        {
            if (_suppressTreeNav) return;
            if (args.NewValue is FolderItem fi && fi.IsDirectory)
            {
                DoNavigateInternal(fi.FullPath, addToHistory: true);
            }
        }

        [RelayCommand]
        private void Search()
        {
            var q = SearchQuery?.Trim();
            if (string.IsNullOrEmpty(q)) return;

            var match = FindMatch(FolderItems, q);
            if (match == null) return;

            var targetPath = match.IsDirectory
                ? match.FullPath
                : Path.GetDirectoryName(match.FullPath)!;

            _suppressTreeNav = true;

            DoNavigateInternal(targetPath, addToHistory: true);

            ExpandAndSelectPath(match.FullPath);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
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
    

    public static class VisualTreeHelperExtensions
    {
        public static T GetAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T correctlyTyped) return correctlyTyped;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
