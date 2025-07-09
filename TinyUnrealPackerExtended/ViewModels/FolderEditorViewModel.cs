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
using ICSharpCode.AvalonEdit.Document;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using TinyUnrealPackerExtended.Extensions;

namespace TinyUnrealPackerExtended.ViewModels
{
    
    public partial class FolderEditorViewModel : ViewModelBase
    {
        private readonly IDialogService _dialog;
        private readonly IBreadcrumbService _breadcrumbs = new BreadcrumbService();
        private readonly ITexturePreviewService _textureService;
        private readonly IFileSystemService _fileSystem;

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

        private readonly List<FolderItem> _searchIndex = new();

        private readonly List<FolderItem> _searchResults = new();
        private int _searchResultsIndex = -1;

        public ObservableCollection<FolderItem> FolderItems { get; } = new();
        public ObservableCollection<BreadcrumbItem> Breadcrumbs => _breadcrumbs.Items;
        public List<BreadcrumbItem> Overflow { get; private set; } = new();


        private FolderItem _clipboardItem;
        private bool _isCut;
        public bool CanPaste => _clipboardItem != null;

        private bool _suppressTreeNav;


        [ObservableProperty] private bool isAlphaEnabled = true;
        private BitmapSource _originalTexture;

        private CancellationTokenSource _loadCts;

        [ObservableProperty]
        private string selectedCodeText;

        // Новое свойство для AvalonEdit
        [ObservableProperty]
        private TextDocument codeDocument = new TextDocument();

        partial void OnSelectedCodeTextChanged(string oldValue, string newValue)
        {
            CodeDocument.Text = newValue ?? string.Empty;
        }


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
            _fileSystem = new FileSystemService();
            _textureService = new TexturePreviewService();
        }

        [RelayCommand]
        private async Task LoadFolderEditorAsync(CancellationToken cancellationToken)
        {
            _loadCts?.Cancel();
            _loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = _loadCts.Token;

            if (string.IsNullOrEmpty(FolderEditorRootPath) || !_fileSystem.DirectoryExists(FolderEditorRootPath))
                return;

            FolderItems.Clear();
            ClearTexture();
            RootFolder = FolderEditorRootPath;

            try
            {
                var rootItem = await _fileSystem.GetTreeAsync(FolderEditorRootPath, ct);
                FolderItems.Add(rootItem);
                SelectedFolderItem = rootItem;

                _searchIndex.Clear();
                BuildSearchIndex(rootItem);

                _breadcrumbs.Initialize(RootFolder);
                _breadcrumbs.OnUpdate += () => OnPropertyChanged(nameof(DisplayBreadcrumbs));
                UpdateNavigationProperties();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void BuildSearchIndex(FolderItem node)
        {
            _searchIndex.Add(node);
            foreach (var child in node.Children)
                BuildSearchIndex(child);
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
        private async Task RefreshFolder(CancellationToken cancellationToken)
        {
            _backStack.Clear();
            _forwardStack.Clear();

            FolderEditorRootPath = RootFolder;
            await LoadFolderEditorAsync(cancellationToken);
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
        private async Task RenameFolderItemAsync(FolderItem item)
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
                await _fileSystem.MoveAsync(oldPath, newPath, CancellationToken.None);

                // Обновляем модель
                item.Name = newName;
                item.FullPath = newPath;

                if (item.IsDirectory)
                    UpdateChildrenPaths(item, oldPath, newPath);
            }
            catch (Exception ex)
            {
                // Показ ошибки
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
        private async Task PasteIntoFolderAsync(FolderItem target)
        {
            if (_clipboardItem == null || target == null)
                return;

            var src = _clipboardItem.FullPath;
            var dest = Path.Combine(target.FullPath, _clipboardItem.Name);

            try
            {
                if (_isCut)
                {
                    await _fileSystem.MoveAsync(src, dest, CancellationToken.None);
                }
                else
                {
                    await _fileSystem.CopyAsync(src, dest, recursive: _clipboardItem.IsDirectory, CancellationToken.None);
                }

                // Добавляем в модель дерева новый узел
                var clone = new FolderItem(
                    _clipboardItem.Name,
                    dest,
                    _clipboardItem.IsDirectory,
                    _clipboardItem.IconKind
                );
                target.Children.Add(clone);

                if (_isCut)
                {
                    // удаляем оригинал из дерева
                    RemoveFromParent(FolderItems, _clipboardItem);
                    _clipboardItem = null;
                }

                OnPropertyChanged(nameof(CanPaste));
            }
            catch (Exception ex)
            {
                // показываем ошибку пользователю
                _growlService.ShowError(ex.Message);
            }
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
        private void ClearPreview()
        {
            SelectedCodeText = null;
            ClearTexture();
        }

        [RelayCommand]
        private async Task PreviewTextureAsync(FolderItem item, CancellationToken ct)
        {
            try
            {
                ClearTexture();
                var bmp = await _textureService.ExtractAsync(item.FullPath, ct);
                _originalTexture = bmp;
                SelectedTexturePreview = _originalTexture;
                PreviewedUassetPath = item.FullPath;
                IsAlphaEnabled = true;
            }
            catch (OperationCanceledException)
            {
                // Предпросмотр отменён
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
        private async Task RemoveFolderItemAsync(FolderItem item)
        {
            if (item == null)
                return;

            // Показываем подтверждение
            bool ok = _dialog.ShowDialog(
               title: "Удалить элемент?",
               message: $"Вы точно хотите удалить «{item.Name}»?",
               dialogType: DialogType.Confirm,
               primaryText: "Да",
               secondaryText: "Нет"
            );
            if (!ok)
                return;

            try
            {
                await _fileSystem.DeleteAsync(
                    path: item.FullPath,
                    recursive: item.IsDirectory,
                    ct: CancellationToken.None
                );

                RemoveFromParent(FolderItems, item);
            }
            catch (Exception ex)
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
        private async Task AddFolderToFolderAsync()
        {
            if (SelectedFolderItem == null || !SelectedFolderItem.IsDirectory)
                return;

            var newDirPath = Path.Combine(SelectedFolderItem.FullPath, "NewFolder");

            try
            {
                await _fileSystem.CreateDirectoryAsync(newDirPath, CancellationToken.None);

                var newItem = new FolderItem(
                    name: Path.GetFileName(newDirPath),
                    fullPath: newDirPath,
                    isDirectory: true,
                    icon: PackIconMaterialKind.FolderOutline
                );
                SelectedFolderItem.Children.Add(newItem);
            }
            catch (Exception ex)
            {
                _growlService.ShowError($"Не удалось создать папку: {ex.Message}");
            }
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
            _dragStartPoint = args.GetPosition(null);
            if (args.OriginalSource is FrameworkElement fe && fe.DataContext is FolderItem fi)
            {
                _draggedFolderItem = fi;
            }
        }

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

        [RelayCommand]
        private async Task OnDropAsync(DragEventArgs args)
        {
            // 1) Перетаскивание своих узлов
            if (args.Data.GetDataPresent("FolderItem") &&
                args.OriginalSource is DependencyObject src &&
                VisualTreeHelperExtensions.GetAncestor<TreeViewItem>(src) is TreeViewItem tvi &&
                tvi.DataContext is FolderItem target)
            {
                var source = args.Data.GetData("FolderItem") as FolderItem;
                if (source != null)
                {
                    var oldPath = source.FullPath;
                    var newPath = Path.Combine(target.FullPath, source.Name);

                    try
                    {
                        await _fileSystem.MoveAsync(oldPath, newPath, CancellationToken.None);

                        RemoveFromParent(FolderItems, source);
                        source.FullPath = newPath;
                        if (source.IsDirectory)
                            UpdateChildrenPaths(source, oldPath, newPath);
                        target.Children.Add(source);

                        _draggedFolderItem = null;
                        _lastTargetFolderItem = null;
                    }
                    catch (Exception ex)
                    {
                        _growlService.ShowError($"Не удалось переместить элемент: {ex.Message}");
                    }

                    args.Handled = true;
                    return;
                }
            }

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
                        await _fileSystem.CopyAsync(srcPath, destPath, recursive: isDir, CancellationToken.None);

                        AddFileIntoFolder(targetExt, destPath);
                    }
                }
                catch (Exception ex)
                {
                    _growlService.ShowError($"Ошибка при вставке файлов: {ex.Message}");
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
        private async Task FileDropAsync(DragEventArgs args)
        {
            if (!args.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var paths = (string[])args.Data.GetData(DataFormats.FileDrop);
            if (SelectedFolderItem == null || !SelectedFolderItem.IsDirectory)
                return;

            try
            {
                foreach (var srcPath in paths)
                {
                    var fileName = Path.GetFileName(srcPath);
                    var destPath = Path.Combine(SelectedFolderItem.FullPath, fileName);

                    bool isDir = _fileSystem.DirectoryExists(srcPath);
                    await _fileSystem.CopyAsync(srcPath, destPath, recursive: isDir, ct: CancellationToken.None);

                    AddFileIntoFolder(SelectedFolderItem, destPath);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при добавлении файлов: {ex.Message}");
            }
            finally
            {
                args.Handled = true;
            }
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

        [RelayCommand(CanExecute = nameof(CanSearch))]
        private void Search()
        {
            var q = SearchQuery.Trim();
            if (string.IsNullOrEmpty(q))
                return;

            if (_searchResultsIndex < 0)
            {
                var cmp = StringComparison.OrdinalIgnoreCase;

                var prefixMatches = _searchIndex
                                        .Where(fi => fi.Name.StartsWith(q, cmp));
                var substringMatches = _searchIndex
                                        .Where(fi => !fi.Name.StartsWith(q, cmp)
                                                  && fi.Name.IndexOf(q, cmp) >= 0);

                _searchResults.Clear();
                _searchResults.AddRange(prefixMatches.Concat(substringMatches));
            }

            if (_searchResults.Count == 0)
            { 
                ShowWarning("Не найдено результатов");
                return;
            }

            _searchResultsIndex = (_searchResultsIndex + 1) % _searchResults.Count;
            var match = _searchResults[_searchResultsIndex];

            var targetPath = match.IsDirectory
                ? match.FullPath
                : Path.GetDirectoryName(match.FullPath)!;

            _suppressTreeNav = true;
            DoNavigateInternal(targetPath, addToHistory: true);
            ExpandAndSelectPath(match.FullPath);

            Application.Current.Dispatcher.BeginInvoke(
                new Action(() => _suppressTreeNav = false),
                DispatcherPriority.Background);
        }

        public bool CanSearch => !string.IsNullOrWhiteSpace(SearchQuery);

        partial void OnSearchQueryChanged(string oldValue, string newValue)
        {
            _searchResults.Clear();
            _searchResultsIndex = -1;
            SearchCommand.NotifyCanExecuteChanged();
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

        private async Task<string> LoadJsonFromAssetAsync(string uassetPath, string rootDir, CancellationToken ct)
        {
            // 1) создаём провайдера и инициализируем
            var provider = new DefaultFileProvider(rootDir, SearchOption.AllDirectories,
                                                   new VersionContainer(EGame.GAME_UE4_LATEST));
            provider.Initialize();

            // 2) находим нужный asset по пути
            var fileName = Path.GetFileName(uassetPath);
            var assetFile = provider.Files.Values
                                  .First(f => f.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            // 3) грузим пакет и получаем DisplayData
            var result = provider.GetLoadPackageResult(assetFile);
            var displayData = result.GetDisplayData(save: false);

            // 4) сериализуем в форматированный JSON
            return JsonConvert.SerializeObject(displayData,
                                               Formatting.Indented,
                                               new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });
        }

        [RelayCommand]
        private async Task PreviewAssetAsync(FolderItem item, CancellationToken ct)
        {
            if (item == null || !item.FullPath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var json = await LoadJsonFromAssetAsync(item.FullPath, FolderEditorRootPath, ct);

                var previewWindow = new CodePreviewWindow
                {
                    Owner = Application.Current.MainWindow
                };

                var previewVm = new CodePreviewViewModel(json, previewWindow);

                previewWindow.DataContext = previewVm;

                // 4) Показываем
                previewWindow.Show();
            }
            catch (Exception ex)
            {
                ShowError($"Не удалось загрузить asset: {ex.Message}");
            }
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
