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

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly GrowlService _growlService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IProcessRunner _processRunner;
        private readonly IFileSystemService _fileSystemService;


        private readonly LocresService _locresService = new();
        private readonly ExcelService _excelService = new();

        [ObservableProperty] private string locresOutputPath;
        [ObservableProperty] private string locresStatusMessage;
        [ObservableProperty] private string excelOutputPath;
        [ObservableProperty] private string excelStatusMessage;

        [ObservableProperty] private bool isLocresBusy;
        [ObservableProperty] private bool isExcelBusy;
        [ObservableProperty] private bool isCsvFileDropped;
        [ObservableProperty] private bool isLocresFileDropped;

        public ObservableCollection<FileItem> PakFiles { get; } = new();
        [ObservableProperty] private bool isPakFolderDropped;
        [ObservableProperty] private bool isPakBusy;
        [ObservableProperty] private string pakStatusMessage;
        [ObservableProperty] private string folderEditorRootPath;
        public ObservableCollection<FolderItem> FolderItems { get; } = new ObservableCollection<FolderItem>();

        [ObservableProperty] private string originalUassetPath;
        public ObservableCollection<FileItem> InjectFiles { get; } = new();
        public ObservableCollection<FileItem> TextureFiles { get; } = new();
        [ObservableProperty] private string injectOutputPath;
        [ObservableProperty] private bool isInjectBusy;
        [ObservableProperty] private string injectStatusMessage;
        public bool HasInjectFile => InjectFiles.Count > 0;
        public bool HasTextureFiles => TextureFiles.Count > 0;

        public ObservableCollection<AutoInjectItem> AutoInjectItems { get; } = new();

        [ObservableProperty] private string autoInjectOutputPath;

        public bool HasAutoInjectItems => AutoInjectItems.Count > 0;


        public ObservableCollection<FileItem> LocresFiles { get; } = new();
        public ObservableCollection<FileItem> OriginalLocresFiles { get; } = new();
        public ObservableCollection<FileItem> ExcelFiles { get; } = new();

        private readonly IDialogService _dialog;

        private FolderItem _clipboardItem;
        private bool _isCut;
        public bool CanPaste => _clipboardItem != null;

        [ObservableProperty] public ObservableCollection<BreadcrumbItem> breadcrumbs = new();
        [ObservableProperty] private string rootFolder;

        [ObservableProperty] private ImageSource _selectedTexturePreview;

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
                int visible = Math.Min(all.Count, Math.Max(4, MaxVisible));
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

        public bool CanEditFolderEditor => PakFiles.Any();


        public MainWindowViewModel(IDialogService dialogService, GrowlService growlService, IFileDialogService fileDialogService,
            IProcessRunner processRunner, IFileSystemService fileSystemService)
        {
            _growlService = growlService;
            _dialog = dialogService;
            _fileDialogService = fileDialogService;
            _processRunner = processRunner;
            _fileSystemService = fileSystemService;
            LocresFiles.CollectionChanged += OnLocresCollectionsChanged;
            OriginalLocresFiles.CollectionChanged += OnLocresCollectionsChanged;

            InjectFiles.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasInjectFile));
            TextureFiles.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasTextureFiles));
            AutoInjectItems.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasAutoInjectItems));
            PakFiles.CollectionChanged += (_, __) => OnPropertyChanged(nameof(CanEditFolderEditor));
            _processRunner = processRunner;
            _fileSystemService = fileSystemService;
        }

        private void OnLocresCollectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            IsLocresFileDropped = LocresFiles.Count > 0 || OriginalLocresFiles.Count > 0;
        }

        [RelayCommand]
        private async Task BrowseLocresInputAsync()
        {
            if (LocresFiles.Count >= 1)
            {
                LocresStatusMessage = "Можно добавить только один файл.";
                return;
            }

            if (await TryPickSingleFileAsync(
                    filter: "Locres or CSV|*.locres;*.csv",
                    title: "Выберите .locres или .csv файл",
                    target: LocresFiles))
            {
                var ext = Path.GetExtension(LocresFiles.First().FilePath)
                               .ToLowerInvariant();
                IsCsvFileDropped = ext == ".csv";
            }
        }



        [RelayCommand]
        private async Task BrowseOriginalLocresAsync()
        {
            if (OriginalLocresFiles.Count >= 1)
            {
                LocresStatusMessage = "Можно добавить только один оригинальный .locres.";
                return;
            }

            if (await TryPickSingleFileAsync(
                    filter: "Original Locres|*.locres",
                    title: "Выберите оригинальный .locres файл",
                    target: OriginalLocresFiles))
            {
                IsCsvFileDropped = false;
            }
        }

        [RelayCommand]
        private Task ProcessLocresAsync(CancellationToken token)
        {
            if (LocresFiles.Count == 0)
            {
                LocresStatusMessage = "Ошибка: укажите файл для обработки.";
                _growlService.ShowWarning(LocresStatusMessage);
                return Task.CompletedTask;
            }

            return ExecuteWithBusyFlagAsync(async ct =>
            {
                var input = LocresFiles.First().FilePath;
                var ext = Path.GetExtension(input).ToLowerInvariant();
                string output;

                if (ext == ".locres")
                {
                    output = Path.ChangeExtension(input, ".csv");
                    await Task.Run(() => _locresService.Export(input, output), ct);
                    LocresStatusMessage = $"Экспорт завершён: {output}";
                }
                else
                {
                    output = OriginalLocresFiles.First().FilePath;
                    await Task.Run(() => _locresService.Import(input, output), ct);
                    LocresStatusMessage = $"Импорт в оригинальный .locres завершён: {output}";
                }

                LocresOutputPath = output;
                _growlService.ShowSuccess(LocresStatusMessage);

            }, b => IsLocresBusy = b, token);
        }

        [RelayCommand]
        private async Task BrowseExcelInputAsync()
        {
            if (ExcelFiles.Count >= 1)
            {
                ExcelStatusMessage = "Можно добавить только один файл.";
                return;
            }

            await TryPickSingleFileAsync(
                filter: "XLSX or CSV|*.xlsx;*.csv",
                title: "Выберите .xlsx или .csv файл",
                target: ExcelFiles);
        }

        [RelayCommand]
        private Task ProcessExcelAsync(CancellationToken token)
        {
            if (ExcelFiles.Count == 0)
            {
                ExcelStatusMessage = "Ошибка: укажите файл для обработки.";
                _growlService.ShowWarning(ExcelStatusMessage);
                return Task.CompletedTask;
            }

            return ExecuteWithBusyFlagAsync(async ct =>
            {
                var input = ExcelFiles.First().FilePath;
                var ext = Path.GetExtension(input).ToLowerInvariant();
                string output;

                switch (ext)
                {
                    case ".xlsx":
                        output = Path.ChangeExtension(input, ".csv");
                        await Task.Run(() => _excelService.ImportFromExcel(input, output), ct);
                        ExcelStatusMessage = $"Импорт из Excel завершён: {output}";
                        break;

                    case ".csv":
                        output = Path.ChangeExtension(input, ".xlsx");
                        await Task.Run(() => _excelService.ExportToExcel(input, output), ct);
                        ExcelStatusMessage = $"Экспорт в Excel завершён: {output}";
                        break;

                    default:
                        ExcelStatusMessage = "Неподдерживаемый формат. Поддерживаются .xlsx и .csv.";
                        _growlService.ShowWarning(ExcelStatusMessage);
                        return;
                }

                ExcelOutputPath = output;
                _growlService.ShowSuccess(ExcelStatusMessage);

            }, b => IsExcelBusy = b, token);
        }


        [RelayCommand]
        private async Task BrowsePakFolderAsync()
        {
            // Выбираем папку через сервис диалогов
            var folder = await _fileDialogService.PickFolderAsync(
                description: "Выберите папку для упаковки");
            if (string.IsNullOrEmpty(folder))
                return;

            RootFolder = folder;
            FolderEditorRootPath = folder;
            PakFiles.Clear();

            PakFiles.Add(new FileItem
            {
                FileName = Path.GetFileName(folder),
                FilePath = folder,
                IconKind = PackIconMaterialKind.FolderOutline
            });

            LoadFolderEditor();
        }

        [RelayCommand]
        private Task ProcessPakAsync(CancellationToken token)
        {
            if (!PakFiles.Any())
            {
                PakStatusMessage = "Укажите папку для упаковки.";
                _growlService.ShowWarning(PakStatusMessage);
                return Task.CompletedTask;
            }

            return ExecuteWithBusyFlagAsync(async ct =>
            {
                var folder = PakFiles.First().FilePath;
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var exeDir = Path.Combine(baseDir, "UnrealPak");
                var exePath = Path.Combine(exeDir, "UnrealPak.exe");
                var listFile = Path.Combine(exeDir, "filelist.txt");

                await File.WriteAllTextAsync(listFile, $"\"{folder}\\*.*\" \"..\\..\\..\\*.*\"", ct);

                var pakName = Path.GetFileName(folder) + ".pak";
                var pakPath = Path.Combine(Path.GetDirectoryName(folder)!, pakName);
                var args = $"\"{pakPath}\" -create=\"{listFile}\"";

                var exitCode = await _processRunner.RunAsync(
                    exePath,
                    arguments: args,
                    workingDirectory: exeDir,
                    cancellationToken: ct
                );

                if (exitCode == 0)
                {
                    PakStatusMessage = $"Упаковано: {pakPath}";
                    _growlService.ShowSuccess(PakStatusMessage);
                }
                else
                {
                    PakStatusMessage = $"Ошибка упаковки (код {exitCode})";
                    _growlService.ShowError(PakStatusMessage);
                }

            }, b => IsPakBusy = b, token);
        }

        [RelayCommand]
        private Task ProcessPakCompressedAsync(CancellationToken token)
        {
            if (!PakFiles.Any())
            {
                PakStatusMessage = "Укажите папку для упаковки.";
                _growlService.ShowWarning(PakStatusMessage);
                return Task.CompletedTask;
            }

            return ExecuteWithBusyFlagAsync(async ct =>
            {
                var folder = PakFiles.First().FilePath;
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var exeDir = Path.Combine(baseDir, "UnrealPak");
                var exePath = Path.Combine(exeDir, "UnrealPak.exe");
                var listFile = Path.Combine(exeDir, "filelist.txt");

                // Генерируем список
                await File.WriteAllTextAsync(
                    listFile,
                    $"\"{folder}\\*.*\" \"..\\..\\..\\*.*\"",
                    ct
                );

                // Формируем путь пак-файла
                var pakName = Path.GetFileName(folder) + ".pak";
                var pakPath = Path.Combine(Path.GetDirectoryName(folder)!, pakName);

                // Здесь добавляем -compress
                var args = $"\"{pakPath}\" -create=\"{listFile}\" -compress";

                // Запускаем UnrealPak
                var exitCode = await _processRunner.RunAsync(
                    exePath,
                    arguments: args,
                    workingDirectory: exeDir,
                    cancellationToken: ct
                );

                if (exitCode == 0)
                {
                    PakStatusMessage = $"Упаковано с компрессором: {pakPath}";
                    _growlService.ShowSuccess(PakStatusMessage);
                }
                else
                {
                    PakStatusMessage = $"Ошибка упаковки (compress) — код {exitCode}";
                    _growlService.ShowError(PakStatusMessage);
                }
            },
            b => IsPakBusy = b,
            token);
        }


        [RelayCommand]
        private async Task BrowseOriginalUassetAsync()
        {
            await TryPickSingleFileAsync(
                filter: "UAsset|*.uasset",
                title: "Выберите .uasset файл",
                target: InjectFiles);
        }

        [RelayCommand]
        private async Task BrowseTextureAsync()
        {
            var paths = await _fileDialogService.PickFilesAsync(
                filter: "Image|*.png;*.jpg;*.tga;*.dds",
                title: "Выберите одну или несколько текстур");
            if (paths is null || paths.Length == 0)
                return;

            TextureFiles.Clear();
            foreach (var p in paths)
            {
                TextureFiles.Add(new FileItem
                {
                    FileName = Path.GetFileName(p),
                    FilePath = p,
                    IconKind = PackIconMaterialKind.ImageOutline
                });
            }
        }

        [RelayCommand]
        private async Task BrowseInjectOutputAsync()
        {
            var folder = await _fileDialogService.PickFolderAsync(
                description: "Выберите папку для вывода");
            if (string.IsNullOrEmpty(folder))
                return;

            InjectOutputPath = folder;
        }

        [RelayCommand]
        private Task ProcessInjectAsync(CancellationToken token)
        {
            if (!InjectFiles.Any() || !TextureFiles.Any() || string.IsNullOrEmpty(InjectOutputPath))
            {
                InjectStatusMessage = "Укажите исходный .uasset, хотя бы одну текстуру и папку вывода.";
                _growlService.ShowWarning(InjectStatusMessage);
                return Task.CompletedTask;
            }

            return ExecuteWithBusyFlagAsync(async ct =>
            {
                var assetPath = InjectFiles.First().FilePath;
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var ddsDir = Path.Combine(baseDir, "DDS");
                var injected = Path.Combine(ddsDir, "injected");

                if (Directory.Exists(injected))
                    Directory.Delete(injected, true);
                Directory.CreateDirectory(injected);

                await File.WriteAllTextAsync(
                    Path.Combine(ddsDir, "src", "_file_path_.txt"),
                    assetPath,
                    ct
                );

                var args = string.Join(" ", TextureFiles.Select(f => $"\"{f.FilePath}\""));

                var exitCode = await _processRunner.RunAsync(
                    exePath: Path.Combine(ddsDir, "_3_inject.bat"),
                    arguments: args,
                    workingDirectory: ddsDir,
                    cancellationToken: ct
                );

                if (exitCode != 0)
                    throw new InvalidOperationException($"Batch вернулся с кодом {exitCode}");

                foreach (var file in Directory.GetFiles(injected))
                {
                    File.Copy(file, Path.Combine(InjectOutputPath, Path.GetFileName(file)!), true);
                }

                InjectStatusMessage = "Инжект окончен.";
                _growlService.ShowSuccess(InjectStatusMessage);

            }, b => IsInjectBusy = b, token);
        }


        public string InjectOutputButtonText
    => string.IsNullOrEmpty(InjectOutputPath)
       ? "Выберите конечный путь"
       : InjectOutputPath;

        partial void OnInjectOutputPathChanged(string oldValue, string newValue)
            => OnPropertyChanged(nameof(InjectOutputButtonText));

        public void LoadAutoFiles(string[] paths)
        {
            var newFiles = paths.Select(p => new
            {
                Path = p,
                Base = System.IO.Path.GetFileNameWithoutExtension(p),
                Ext = System.IO.Path.GetExtension(p).ToLowerInvariant()
            });

            var groups = newFiles.GroupBy(f => f.Base);

            foreach (var g in groups)
            {
                if (AutoInjectItems.Any(item => item.Name == g.Key))
                    continue;

                var asset = g.FirstOrDefault(x => x.Ext == ".uasset");
                var tex = g.FirstOrDefault(x => x.Ext == ".png");
                if (asset != null && tex != null)
                {
                    AutoInjectItems.Add(new AutoInjectItem
                    {
                        Name = g.Key,
                        AssetFile = new FileItem
                        {
                            FileName = System.IO.Path.GetFileName(asset.Path),
                            FilePath = asset.Path
                        },
                        TextureFile = new FileItem
                        {
                            FileName = System.IO.Path.GetFileName(tex.Path),
                            FilePath = tex.Path
                        },
                        Status = "Загружено"
                    });
                }
            }
        }


        [RelayCommand]
        private async Task BrowseAutoFilesAsync()
        {
            var paths = await _fileDialogService.PickFilesAsync(
                filter: "UAsset & PNG|*.uasset;*.png",
                title: "Выберите .uasset и соответствующие .png");
            if (paths is null || paths.Length == 0)
                return;

            LoadAutoFiles(paths);
        }

        [RelayCommand]
        private async Task BrowseAutoOutputAsync()
        {
            var folder = await _fileDialogService.PickFolderAsync(
                description: "Выберите папку вывода");
            if (string.IsNullOrEmpty(folder))
                return;

            AutoInjectOutputPath = folder;
        }


        [RelayCommand]
        private async Task ProcessAutoInjectAsync()
        {
            if (!AutoInjectItems.Any())
            {
                _growlService.ShowWarning("Нет подготовленных пар для инжекта.");
                return;
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var ddsDir = Path.Combine(baseDir, "DDS");
            var injectedDir = Path.Combine(ddsDir, "injected");

            if (Directory.Exists(injectedDir))
                Directory.Delete(injectedDir, true);
            Directory.CreateDirectory(injectedDir);

            foreach (var item in AutoInjectItems)
            {
                item.Status = "В процессе...";
                try
                {
                    File.WriteAllText(
                        Path.Combine(ddsDir, "src", "_file_path_.txt"),
                        item.AssetFile.FilePath
                    );

                    var args = $"\"{item.TextureFile.FilePath}\"";

                    var exitCode = await _processRunner.RunAsync(
                        exePath: Path.Combine(ddsDir, "_3_inject.bat"),
                        arguments: args,
                        workingDirectory: ddsDir
                    );

                    if (exitCode != 0)
                        throw new InvalidOperationException($"Batch вернулся с кодом {exitCode}");

                    if (!string.IsNullOrEmpty(AutoInjectOutputPath))
                    {
                        foreach (var file in Directory.GetFiles(injectedDir))
                            File.Copy(file, Path.Combine(AutoInjectOutputPath, Path.GetFileName(file)!), true);
                    }

                    item.Status = "Готово";
                }
                catch (Exception ex)
                {
                    item.Status = "Ошибка";
                    _growlService.ShowError($"{item.Name}: {ex.Message}");
                }
            }

            _growlService.ShowSuccess("Автоинжект закончен.");
        }


        public string AutoInjectOutputButtonText => string.IsNullOrEmpty(AutoInjectOutputPath)
    ? "Выберите конечный путь"
    : AutoInjectOutputPath;

        partial void OnAutoInjectOutputPathChanged(string oldValue, string newValue)
        {
            OnPropertyChanged(nameof(AutoInjectOutputButtonText));
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

            // если это файл — откроем его родительскую папку и выделим файл
            // если папка — просто откроем ее
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
                // здесь можно вывести ошибку пользователю
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
            LoadFolderEditor();
        }

        partial void OnSelectedFolderItemChanged(FolderItem newItem)
        {
            if (SelectedTexturePreview != null)
            {
                SelectedTexturePreview = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        private async Task LoadTexturePreviewAsync(string uassetPath)
        {
            try
            {
                var imageSource = await Task.Run(() =>
                {
                    // 1) Папка, где лежит ваш .uasset
                    var fullPath = Path.GetFullPath(uassetPath);
                    var assetDir = Path.GetDirectoryName(fullPath)!;
                    var fileName = Path.GetFileName(fullPath);

                    // 2) Создаём провайдер только для этой папки (без рекурсии!)
                    var provider = new DefaultFileProvider(
                        assetDir,
                        SearchOption.TopDirectoryOnly,                   // только одна папка
                        new VersionContainer(EGame.GAME_UE4_LATEST)
                    );
                    provider.Initialize();  // будет индексировать только файлы рядом с uasset

                    // 3) Находим единственный ключ, оканчивающийся нашим именем
                    var key = provider.Files.Keys
                                  .FirstOrDefault(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
                    if (key == null)
                        throw new FileNotFoundException($"Asset не найден: {fileName}");

                    // 4) Загружаем пакет и вытаскиваем первую текстуру
                    var package = provider.LoadPackage(key);
                    var texture = package.ExportsLazy
                                          .Select(l => l.Value)
                                          .OfType<UTexture2D>()
                                          .FirstOrDefault();
                    if (texture == null)
                        return null;

                    // 5) Декодируем в SKBitmap и сразу его освобождаем по окончании блока
                    using var skBitmap = texture.Decode(ETexturePlatform.DesktopMobile);
                    if (skBitmap == null)
                        return null;

                    // 6) Конвертируем SKBitmap → WPF BitmapImage без лишних временных буферов
                    using var skData = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
                    using var ms = new MemoryStream(skData.ToArray());

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bmp.EndInit();
                    bmp.Freeze();

                    return (ImageSource)bmp;
                });

                SelectedTexturePreview = imageSource;
            }
            catch
            {
                SelectedTexturePreview = null;
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
            // guard: must be a .uasset file
            if (item == null || item.IsDirectory ||
                !Path.GetExtension(item.FullPath).Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                _growlService.ShowWarning("Выберите .uasset с текстурой");
                return;
            }

            BitmapImage bmp;
            try
            {
                // reuse your extractor
                bmp = await ExtractTextureAsync(item.FullPath);
                if (bmp == null) throw new InvalidOperationException();
            }
            catch
            {
                _growlService.ShowError("Не удалось извлечь изображение из .uasset");
                return;
            }

            // set the placeholder-bound property
            SelectedTexturePreview = bmp;
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
            if (source == null || target == null || source == target) return;
            var newPath = Path.Combine(target.FullPath, source.Name);
            if (source.IsDirectory)
                Directory.Move(source.FullPath, newPath);
            else
                File.Move(source.FullPath, newPath);

            // снимаем из старого родителя
            RemoveFromParent(FolderItems, source);
            // добавляем в новый родитель
            source.FullPath = newPath;
            target.Children.Add(source);
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
        private void RemoveAutoInjectItem(AutoInjectItem item)
        {
            if (item != null && AutoInjectItems.Contains(item))
                AutoInjectItems.Remove(item);
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

        [RelayCommand]
        private void RemoveFile(FileItem file)
        {
            LocresFiles.Remove(file);
            OriginalLocresFiles.Remove(file);
            ExcelFiles.Remove(file);
            PakFiles.Remove(file);
            InjectFiles.Remove(file);
            TextureFiles.Remove(file);
        }

        [RelayCommand]
        private void CancelLocres()
        {
            // Сбрасываем все данные и флаги для Locres
            LocresFiles.Clear();
            OriginalLocresFiles.Clear();
            IsCsvFileDropped = false;
            IsLocresFileDropped = false;
            IsLocresBusy = false;
            LocresStatusMessage = string.Empty;
            LocresOutputPath = string.Empty;
        }

        [RelayCommand]
        private void CancelExcel()
        {
            // Сбрасываем все данные и флаги для Excel
            ExcelFiles.Clear();
            IsExcelBusy = false;
            ExcelStatusMessage = string.Empty;
            ExcelOutputPath = string.Empty;
        }

        [RelayCommand]
        private void CancelPak()
        {
            PakFiles.Clear();
            pakStatusMessage = string.Empty;
            IsPakBusy = false;
        }

        [RelayCommand]
        private void CancelInject()
        {
            InjectFiles.Clear();
            TextureFiles.Clear();
            InjectOutputPath = string.Empty;
            IsInjectBusy = false;
        }

        private async Task<bool> TryPickSingleFileAsync(string filter, string title, ObservableCollection<FileItem> target)
        {
            var path = await _fileDialogService.PickFileAsync(filter: filter, title: title);
            if (path is null) return false;
            target.Clear();
            target.Add(new FileItem
            {
                FileName = Path.GetFileName(path),
                FilePath = path,
                IconKind = PackIconMaterialKind.FileDocumentOutline
            });
            return true;
        }

        private async Task ExecuteWithBusyFlagAsync(
    Func<CancellationToken, Task> operation,
    Action<bool> setBusy,
    CancellationToken cancellationToken)
        {
            try
            {
                setBusy(true);
                await operation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _growlService.ShowWarning("Операция отменена пользователем.");
            }
            catch (Exception ex)
            {
                _growlService.ShowError($"Ошибка: {ex.Message}");
            }
            finally
            {
                setBusy(false);
            }
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
