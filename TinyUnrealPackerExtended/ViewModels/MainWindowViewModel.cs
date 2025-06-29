using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.Assets.Exports;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Textures;
using System.Windows.Media.Imaging;
using SkiaSharp;
using TinyUnrealPackerExtended.Helpers;
using SevenZip.Compression.LZ;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly GrowlService _growlService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IProcessRunner _processRunner;
        private readonly IFileSystemService _fileSystemService;

        private readonly FullscreenHelper _fullscreenHelper;


        public LocresViewModel LocresVM { get; }
        public ExcelViewModel ExcelVM { get; }
        public PakViewModel PakVM { get; }
        public UassetInjectorViewModel UassetInjectorVM { get; }
        public AutoInjectViewModel AutoInjectVM { get; }


        private readonly LocresService _locresService = new();
        private readonly ExcelService _excelService = new();

        [ObservableProperty] private string folderEditorRootPath;
        public ObservableCollection<FolderItem> FolderItems { get; } = new ObservableCollection<FolderItem>();

        private readonly IDialogService _dialog;

        private FolderItem _clipboardItem;
        private bool _isCut;
        public bool CanPaste => _clipboardItem != null;

        [ObservableProperty] public ObservableCollection<BreadcrumbItem> breadcrumbs = new();
        [ObservableProperty] private string rootFolder;

        private readonly Stack<string> _backStack = new();
        private readonly Stack<string> _forwardStack = new Stack<string>();

        [ObservableProperty] private bool canGoBack;
        [ObservableProperty] private bool canGoForward;


        private bool _suppressHistory = false;

        [ObservableProperty] private ImageSource _selectedTexturePreview;
        [ObservableProperty] private string previewedUassetPath;

        private int _maxVisible = int.MaxValue;
        public int MaxVisible
        {
            get => _maxVisible;
            set
            {
                if (_maxVisible != value)
                {
                    _maxVisible = value;
                    OnPropertyChanged();
                    UpdateBreadcrumbs();
                }
            }
        }

        public IEnumerable<BreadcrumbItem> DisplayBreadcrumbs
        {
            get
            {
                var all = Breadcrumbs.ToList();
                int visible = Math.Min(all.Count, Math.Max(8, MaxVisible));
                if (all.Count <= visible)
                    return all;

                // формируем список скрытых (overflow)
                Overflow = all
                    .Skip(1)
                    .Take(all.Count - visible + 1)
                    .ToList();

                var result = new List<BreadcrumbItem>();
                // всегда первый
                result.Add(all.First());
                // кнопка «…»
                result.Add(new BreadcrumbItem { Name = "…", IsOverflow = true });
                // последние visible-1 элементов
                var lastItems = all.Skip(all.Count - (visible - 1)).ToList();
                result.AddRange(lastItems);
                return result;
            }
        }

        public List<BreadcrumbItem> Overflow { get; private set; } = new();

        public bool CanEditFolderEditor => PakVM.PakFiles.Any();


        public MainWindowViewModel(IDialogService dialogService, GrowlService growlService, IFileDialogService fileDialogService,
            IProcessRunner processRunner, IFileSystemService fileSystemService, System.Windows.Window window)
        {
            _growlService = growlService;
            _dialog = dialogService;
            _fileDialogService = fileDialogService;
            _processRunner = processRunner;
            _fileSystemService = fileSystemService;

            LocresVM = new LocresViewModel(_locresService, growlService, fileDialogService);
            ExcelVM = new ExcelViewModel(_excelService, growlService, fileDialogService);
            PakVM = new PakViewModel(_fileDialogService, growlService, _processRunner);
            UassetInjectorVM = new UassetInjectorViewModel(_fileDialogService, growlService, _processRunner);
            AutoInjectVM = new AutoInjectViewModel(_fileDialogService, growlService, _processRunner);

            PakVM.PakFiles.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(CanEditFolderEditor));

                if (PakVM.PakFiles.Count > 0)
                {
                    var folder = PakVM.PakFiles.First().FilePath;
                    RootFolder = folder;
                    FolderEditorRootPath = folder;
                    LoadFolderEditor();
                }
            };

            _fullscreenHelper = new FullscreenHelper(window);


        }

        [RelayCommand] 
        private void MaximizeWindow()
        {
            _fullscreenHelper.ToggleFullscreen();
        }

        [RelayCommand] 
        private void MinimizeWindow()
        {
            _fullscreenHelper.ToggleMinimizeScreen();
        }


        [ObservableProperty]
        private FolderItem selectedFolderItem;

        [RelayCommand]
        private void LoadFolderEditor()
        {
            if (string.IsNullOrEmpty(FolderEditorRootPath) || !Directory.Exists(FolderEditorRootPath))
                return;

            FolderItems.Clear();
            var rootInfo = new DirectoryInfo(FolderEditorRootPath);
            FolderItems.Add(BuildTreeItem(rootInfo));

            UpdateBreadcrumbs();
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

        private FolderItem? FindFolderItem(string path, IEnumerable<FolderItem> source)
        {
            foreach (var node in source)
            {
                if (node.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                    return node;
                if (node.Children != null)
                {
                    var child = FindFolderItem(path, node.Children);
                    if (child != null)
                        return child;
                }
            }
            return null;
        }

        private void UpdateNavigationProperties()
        {
            CanGoBack = _backStack.Count > 0;
            CanGoForward = _forwardStack.Count > 0;

            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }

        private void DoNavigate(string path, bool fromHistory)
        {
            if (!fromHistory)
            {
                _backStack.Push(FolderEditorRootPath);
                _forwardStack.Clear();
            }

            FolderEditorRootPath = path;
            SelectedFolderItem = FindFolderItem(path, FolderItems)
                                     ?? SelectedFolderItem; 

            UpdateBreadcrumbs();

            UpdateNavigationProperties();

            GoBackCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void NavigateTo(string path)
    => DoNavigate(path, fromHistory: false);

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private void GoBack()
        {
            var prev = _backStack.Pop();
            _forwardStack.Push(FolderEditorRootPath);
            DoNavigate(prev, fromHistory: true);
        }

        [RelayCommand(CanExecute = nameof(CanGoForward))]
        private void GoForward()
        {
            if (_forwardStack.Count == 0) return;

            var next = _forwardStack.Pop();
            _backStack.Push(FolderEditorRootPath);
            DoNavigate(next, fromHistory: true);
        }

        [RelayCommand]
        private void NavigateToBreadcrumb(string path)
        {


            FolderEditorRootPath = path;

            var found = FindFolderItem(path, FolderItems);
            if (found != null)
                SelectedFolderItem = found;

            UpdateBreadcrumbs();
        }

        [RelayCommand]
        private void RefreshFolder()
        {
            if (!string.IsNullOrEmpty(FolderEditorRootPath))
                _backStack.Clear();
                _forwardStack.Clear();

            FolderEditorRootPath = RootFolder;
            LoadFolderEditor();

            SelectedFolderItem = FolderItems.FirstOrDefault();

            UpdateNavigationProperties();
        }

        partial void OnSelectedFolderItemChanged(FolderItem newItem)
        {
            ClearTexture();
        }

        private void ClearTexture()
        {
            if (SelectedTexturePreview != null)
            {
                SelectedTexturePreview = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }



        public async Task<BitmapImage> ExtractTextureAsync(string uassetPath)
        {
            return await Task.Run(() =>
            {
                // 1) normalize paths
                var fullPath = Path.GetFullPath(uassetPath);
                var assetDir = Path.GetDirectoryName(fullPath)!;
                var fileName = Path.GetFileName(fullPath);

                // 2) provider scoping
                using var provider = new DefaultFileProvider(
                    assetDir,
                    SearchOption.TopDirectoryOnly,
                    new VersionContainer(EGame.GAME_UE4_LATEST)
                );
                provider.Initialize();

                // 3) find our package key
                var key = provider.Files.Keys
                             .FirstOrDefault(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                          ?? throw new FileNotFoundException($"Asset not found: {fileName}");

                // 4) load package & grab first Texture2D
                var pkg = provider.LoadPackage(key);
                var texture = pkg.ExportsLazy
                                 .Select(e => e.Value)
                                 .OfType<UTexture2D>()
                                 .FirstOrDefault();
                if (texture == null) return null;

                // 5) decode to SKBitmap
                using var skBmp = texture.Decode(ETexturePlatform.DesktopMobile);
                if (skBmp == null) return null;

                // 5.1) down-sample if larger than 512×512
                const int MAX = 512;
                SKBitmap toEncode = skBmp;
                if (skBmp.Width > MAX || skBmp.Height > MAX)
                {
                    float scale = Math.Min((float)MAX / skBmp.Width, (float)MAX / skBmp.Height);
                    int w = (int)(skBmp.Width * scale), h = (int)(skBmp.Height * scale);
                    var resized = skBmp.Resize(new SKImageInfo(w, h), SKFilterQuality.Medium)
                                  ?? skBmp; // fallback
                    toEncode = resized;
                }

                // 6) encode to PNG bytes
                using var imgData = toEncode.Encode(SKEncodedImageFormat.Png, 100);
                if (toEncode != skBmp) toEncode.Dispose();

                // 7) stream into WPF BitmapImage
                using var ms = new MemoryStream(imgData.ToArray());
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bmp.EndInit();
                bmp.Freeze();

                return bmp;
            });
        }


        [RelayCommand]
        private async Task PreviewTextureAsync(FolderItem item)
        {
            // guard: .uasset
            if (item == null || item.IsDirectory ||
                !Path.GetExtension(item.FullPath)
                     .Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                _growlService.ShowWarning("Выберите .uasset с текстурой");
                return;
            }

            PreviewedUassetPath = item?.FullPath;

            ClearTexture();

            BitmapImage bmp;
            try
            {
                bmp = await ExtractTextureAsync(item.FullPath)
                          ?? throw new InvalidOperationException();
            }
            catch
            {
                _growlService.ShowError("Не удалось извлечь изображение из .uasset");
                return;
            }

            SelectedTexturePreview = bmp;
        }

        [RelayCommand]
        private void ClearTexturePreview()
        {
            ClearTexture();
        }



        [RelayCommand]
        private void RenameFolderItem(FolderItem item)
        {
            if (item == null)
                return;

            // Сохраняем старый путь для рекурсивного обновления потомков
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

            // Формируем новый путь на диске
            var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newName);

            try
            {
                // Физически переименовываем файл или папку
                if (item.IsDirectory)
                    Directory.Move(oldPath, newPath);
                else
                    File.Move(oldPath, newPath);

                // 1) Обновляем свойства самого узла — теперь они вызовут PropertyChanged
                item.Name = newName;
                item.FullPath = newPath;

                // 2) Рекурсивно обновляем FullPath у всех вложенных элементов
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

        // Вспомогательный метод — должен находиться в том же классе MainWindowViewModel
        private void UpdateChildrenPaths(FolderItem parent, string oldParentPath, string newParentPath)
        {
            foreach (var child in parent.Children)
            {
                // Новый путь строим на основе неизменившегося child.Name
                var newChildPath = Path.Combine(newParentPath, child.Name);
                child.FullPath = newChildPath;

                if (child.IsDirectory)
                    UpdateChildrenPaths(child,
                                        oldParentPath: Path.Combine(oldParentPath, child.Name),
                                        newParentPath: newChildPath);
            }
        }

        [RelayCommand]
        private void CopyFolderItem(FolderItem item)
        {
            _clipboardItem = item;
            _isCut = false;
            OnPropertyChanged(nameof(CanPaste));
        }

        [RelayCommand]
        private void CutFolderItem(FolderItem item)
        {

            _clipboardItem = item;
            _isCut = true;
            OnPropertyChanged(nameof(CanPaste));
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

        [RelayCommand]
        private void ShowProperties(FolderItem item)
        {
            if (item == null) return;
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FullPath}\"") { UseShellExecute = true });
        }

        public void MoveFolderItem(FolderItem source, FolderItem target)
        {
            try
            {

                if (source == null || target == null || source == target) return;
                var newPath = Path.Combine(target.FullPath, source.Name);
                if (source.IsDirectory)
                    Directory.Move(source.FullPath, newPath);
                else
                    File.Move(source.FullPath, newPath);

                RemoveFromParent(FolderItems, source);
                source.FullPath = newPath;
                target.Children.Add(source);
            }
            catch (Exception ex)
            {
                _growlService.ShowError(ex.Message);
            }
        }



        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
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
        private void CopyPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;
            Clipboard.SetText(fullPath);
        }

        [RelayCommand]
        private async Task SaveTextureFromUassetAsync()
        {
            if (string.IsNullOrEmpty(PreviewedUassetPath))
                return;

            // Запускаем декодирование в фоновом потоке и возвращаем сразу байты + размеры
            var result = await Task.Run(() =>
            {
                // 1) Декодируем uasset → SKBitmap
                var dir = Path.GetDirectoryName(PreviewedUassetPath)!;
                using var provider = new DefaultFileProvider(
                    dir,
                    SearchOption.TopDirectoryOnly,
                    new VersionContainer(EGame.GAME_UE4_LATEST)
                );
                provider.Initialize();

                var key = provider.Files.Keys
                            .FirstOrDefault(k => k.EndsWith(Path.GetFileName(PreviewedUassetPath),
                                                           StringComparison.OrdinalIgnoreCase))
                          ?? throw new FileNotFoundException(PreviewedUassetPath);
                var pkg = provider.LoadPackage(key);
                var tex = pkg.ExportsLazy
                             .Select(e => e.Value)
                             .OfType<UTexture2D>()
                             .FirstOrDefault()
                          ?? throw new InvalidDataException("Texture not found in uasset");

                using var skBmp = tex.Decode(ETexturePlatform.DesktopMobile)
                                   ?? throw new InvalidOperationException("Decode failed");

                // Сохраняем оригинальные размеры сразу
                int w = skBmp.Width;
                int h = skBmp.Height;

                // 2) Кодируем SKBitmap в PNG
                using var imgData = skBmp.Encode(SKEncodedImageFormat.Png, 100);
                var bytes = imgData.ToArray();

                return (bytes, w, h);
            });

            // Распаковываем результат
            var pngBytes = result.bytes;
            var width = result.w;
            var height = result.h;

            // Формируем имя по шаблону: OriginalName_WIDTHxHEIGHT.png
            var baseName = Path.GetFileNameWithoutExtension(PreviewedUassetPath);
            var defaultName = $"{baseName}.png";

            // Открываем диалог «Сохранить как…»
            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                FileName = defaultName
            };
            if (dlg.ShowDialog() != true)
                return;

            // 3) Пишем файл
            await File.WriteAllBytesAsync(dlg.FileName, pngBytes);
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

        private FolderItem BuildTreeItem(DirectoryInfo dir)
        {
            // Заменили на использование нового конструктора и установки иконки
            var node = new FolderItem(
                name: dir.Name,
                fullPath: dir.FullName,
                isDirectory: true,
                icon: PackIconMaterialKind.FolderOutline
            );

            // подпапки
            foreach (var subDir in dir.GetDirectories())
                node.Children.Add(BuildTreeItem(subDir));

            // файлы
            foreach (var file in dir.GetFiles())
                node.Children.Add(new FolderItem(
                    name: file.Name,
                    fullPath: file.FullName,
                    isDirectory: false,
                    icon: PackIconMaterialKind.FileOutline
                ));

            return node;
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

        public void UpdateBreadcrumbs()
        {
            var all = new List<BreadcrumbItem>();

            if (string.IsNullOrWhiteSpace(RootFolder))
            {
                Breadcrumbs.Clear();
                Overflow.Clear();
                OnPropertyChanged(nameof(DisplayBreadcrumbs));
                OnPropertyChanged(nameof(Overflow));
                return;
            }

            all.Add(new BreadcrumbItem
            {
                Name = Path.GetFileName(RootFolder.TrimEnd(Path.DirectorySeparatorChar)),
                FullPath = RootFolder
            });

            if (!FolderEditorRootPath.Equals(RootFolder, StringComparison.OrdinalIgnoreCase))
            {
                var rel = FolderEditorRootPath
                            .Substring(RootFolder.Length)
                            .Trim(Path.DirectorySeparatorChar);
                var parts = rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

                var accum = RootFolder;
                foreach (var part in parts)
                {
                    accum = Path.Combine(accum, part);
                    all.Add(new BreadcrumbItem
                    {
                        Name = part,
                        FullPath = accum
                    });
                }
            }

            Breadcrumbs.Clear();
            foreach (var b in all)
                Breadcrumbs.Add(b);

            if (all.Count <= MaxVisible)
            {
                Overflow.Clear();
            }
            else
            {
                Overflow = all
                    .Skip(1)
                    .Take(all.Count - MaxVisible + 1)
                    .ToList();
            }

            OnPropertyChanged(nameof(DisplayBreadcrumbs));
            OnPropertyChanged(nameof(Overflow));
        }
    }

    public class FileItem
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public PackIconMaterialKind IconKind { get; set; } = PackIconMaterialKind.FileDocumentOutline;
    }

    public partial class FolderItem : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private string fullPath;
        public bool IsDirectory { get; set; }
        public PackIconMaterialKind IconKind { get; set; }

        public ObservableCollection<FolderItem> Children { get; }
            = new ObservableCollection<FolderItem>();

        public FolderItem() { }

        public FolderItem(string name, string fullPath, bool isDirectory, PackIconMaterialKind icon)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
            IconKind = icon;
        }

        // ← Добавляем эти два свойства ↓
        public string DateModified
            => File.GetLastWriteTime(FullPath).ToString("g");

        public string Size
            => !IsDirectory
               ? $"{new FileInfo(FullPath).Length / 1024:n0} KB"
               : string.Empty;
    }

    public partial class AutoInjectItem : ObservableObject
    {
        public string Name { get; set; }
        public FileItem AssetFile { get; set; }
        public FileItem TextureFile { get; set; }

        [ObservableProperty]
        private string status;
    }

    public class BreadcrumbItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }

        public bool IsOverflow { get; set; } = false;
    }
}
