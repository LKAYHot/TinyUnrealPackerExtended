using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;
using System.Diagnostics;
using HandyControl.Controls;
using System.Windows;

namespace TinyUnrealPackerExtended.ViewModels
{
    /// <summary>
    /// ViewModel for Folder Editor: directory browsing, navigation, breadcrumbs and .uasset texture preview.
    /// Commands generated via CommunityToolkit [RelayCommand].
    /// </summary>
    public partial class FolderEditorViewModel : ViewModelBase
    {
        private readonly IFileDialogService _fileDialog;
        private readonly IDialogService _dialog;
        private readonly Stack<string> _backStack = new();
        private readonly Stack<string> _forwardStack = new();

        [ObservableProperty] private string rootFolder;
        [ObservableProperty] private string folderEditorRootPath;
        [ObservableProperty] private FolderItem selectedFolderItem;
        [ObservableProperty] private bool canGoBack;
        [ObservableProperty] private bool canGoForward;
        [ObservableProperty] private int maxVisible = int.MaxValue;

        [ObservableProperty] private bool canEditFolderEditor;

        [ObservableProperty] private ImageSource selectedTexturePreview;
        [ObservableProperty] private string previewedUassetPath;

        public ObservableCollection<FolderItem> FolderItems { get; } = new();
        public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();
        public List<BreadcrumbItem> Overflow { get; private set; } = new();

        private FolderItem _clipboardItem;
        private bool _isCut;
        public bool CanPaste => _clipboardItem != null;

        /// <summary>
        /// Exposes breadcrumbs with overflow handling.
        /// </summary>
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
        }

        // Directory loading and navigation
        [RelayCommand]
        private void LoadFolderEditor()
        {
            if (string.IsNullOrEmpty(FolderEditorRootPath) || !Directory.Exists(FolderEditorRootPath))
                return;

            FolderItems.Clear();
            RootFolder = FolderEditorRootPath;
            FolderItems.Add(BuildTreeItem(new DirectoryInfo(FolderEditorRootPath)));
            UpdateBreadcrumbs(isInitial: true);
            UpdateNavigationProperties();
        }

        [RelayCommand]
        private void NavigateTo(string path) => DoNavigate(path, false);

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private void GoBack()
        {
            if (_backStack.Count == 0) return;
            var prev = _backStack.Pop();
            _forwardStack.Push(FolderEditorRootPath);
            DoNavigate(prev, true);
        }

        [RelayCommand(CanExecute = nameof(CanGoForward))]
        private void GoForward()
        {
            if (_forwardStack.Count == 0) return;
            var next = _forwardStack.Pop();
            _backStack.Push(FolderEditorRootPath);
            DoNavigate(next, true);
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

            // Спрашиваем новое имя у пользователя
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
            UpdateBreadcrumbs();
        }

        private void DoNavigate(string path, bool fromHistory)
        {
            if (!fromHistory && !string.IsNullOrEmpty(FolderEditorRootPath))
            {
                _backStack.Push(FolderEditorRootPath);
                _forwardStack.Clear();
            }

            FolderEditorRootPath = path;
            SelectedFolderItem = FindFolderItem(path, FolderItems) ?? SelectedFolderItem;
            UpdateBreadcrumbs();
            UpdateNavigationProperties();
        }

        private void UpdateNavigationProperties()
        {
            CanGoBack = _backStack.Count > 0;
            CanGoForward = _forwardStack.Count > 0;
        }

        private void UpdateBreadcrumbs(bool isInitial = false)
        {
            Breadcrumbs.Clear();
            Overflow.Clear();

            if (string.IsNullOrWhiteSpace(RootFolder))
                return;

            var all = new List<BreadcrumbItem>
            {
                 new BreadcrumbItem
                 {
                    Name = Path.GetFileName(RootFolder.TrimEnd(Path.DirectorySeparatorChar)),
                     FullPath = RootFolder
                 }
            };

            if (!FolderEditorRootPath.Equals(RootFolder, StringComparison.OrdinalIgnoreCase))
            {
                var accumPath = RootFolder.TrimEnd(Path.DirectorySeparatorChar);
                var rel = FolderEditorRootPath
                              .Substring(accumPath.Length)
                              .Trim(Path.DirectorySeparatorChar);

                foreach (var part in rel.Split(Path.DirectorySeparatorChar))
                {
                    accumPath = Path.Combine(accumPath, part);
                    all.Add(new BreadcrumbItem
                    {
                        Name = part,
                        FullPath = accumPath
                    });
                }
            }

            foreach (var b in all)
                Breadcrumbs.Add(b);

            if (all.Count <= MaxVisible)
            {
                Overflow.Clear();
            }
            else if (!isInitial)
            {
                Overflow = all
                    .Skip(1)
                    .Take(all.Count - MaxVisible + 1)
                    .ToList();
            }

            OnPropertyChanged(nameof(DisplayBreadcrumbs));
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
        private async Task PreviewTexture(FolderItem item)
        {
            if (item == null || !item.FullPath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                ShowWarning("Выберите .uasset для предпросмотра");
                return;
            }
            PreviewedUassetPath = item.FullPath;
            ClearTexture();
            try
            {
                SelectedTexturePreview = await ExtractTextureAsync(item.FullPath);
            }
            catch
            {
                ShowError("Не удалось извлечь текстуру");
            }
        }

        private async Task<BitmapImage> ExtractTextureAsync(string uassetPath)
        {
            return await Task.Run(() =>
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(uassetPath));
                var fileName = Path.GetFileName(uassetPath);
                using var provider = new DefaultFileProvider(dir, SearchOption.TopDirectoryOnly, new VersionContainer(EGame.GAME_UE4_LATEST));
                provider.Initialize();
                var key = provider.Files.Keys.FirstOrDefault(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                          ?? throw new FileNotFoundException(fileName);
                var pkg = provider.LoadPackage(key);
                var tex = pkg.ExportsLazy.Select(e => e.Value).OfType<UTexture2D>().FirstOrDefault()
                          ?? throw new InvalidDataException("Texture not found");
                using var bmp = tex.Decode(ETexturePlatform.DesktopMobile)
                                  ?? throw new InvalidOperationException("Decode failed");
                const int MAX = 512;
                SKBitmap toEnc = (bmp.Width > MAX || bmp.Height > MAX)
                    ? bmp.Resize(new SKImageInfo(
                        (int)(bmp.Width * Math.Min((float)MAX / bmp.Width, (float)MAX / bmp.Height)),
                        (int)(bmp.Height * Math.Min((float)MAX / bmp.Width, (float)MAX / bmp.Height))
                      ),
                      SKFilterQuality.Medium) ?? bmp
                    : bmp;
                using var img = toEnc.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream(img.ToArray());
                var result = new BitmapImage();
                result.BeginInit(); result.StreamSource = ms; result.CacheOption = BitmapCacheOption.OnLoad; result.CreateOptions = BitmapCreateOptions.PreservePixelFormat; result.EndInit(); result.Freeze();
                return result;
            });
        }

        [RelayCommand]
        private async Task SaveTextureFromUasset()
        {
            if (string.IsNullOrEmpty(PreviewedUassetPath)) return;
            byte[] data;
            try
            {
                var img = await ExtractTextureAsync(PreviewedUassetPath);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(img));
                using var ms = new MemoryStream(); encoder.Save(ms); data = ms.ToArray();
            }
            catch
            {
                ShowError("Ошибка подготовки текстуры");
                return;
            }

            var saveDlg = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                FileName = Path.GetFileNameWithoutExtension(PreviewedUassetPath) + ".png"
            };
            if (saveDlg.ShowDialog() != true) return;
            await File.WriteAllBytesAsync(saveDlg.FileName, data);
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
    }
}
