using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;

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

        const int MAX_VISIBLE = 5;

        public IEnumerable<BreadcrumbItem> DisplayBreadcrumbs
        {
            get
            {
                var all = Breadcrumbs.ToList();
                if (all.Count <= MAX_VISIBLE)
                    return all;

                var first = all.First();
                var lastItems = all.Skip(all.Count - (MAX_VISIBLE - 1)).ToList();

                // сохраняем скрытые внутрь «…» списка
                Overflow = all.Skip(1)
                              .Take(all.Count - MAX_VISIBLE + 1)
                              .ToList();

                var result = new List<BreadcrumbItem> { first };
                // «…»
                result.Add(new BreadcrumbItem { Name = "…", IsOverflow = true });
                result.AddRange(lastItems);
                return result;
            }
        }

        public List<BreadcrumbItem> Overflow { get; private set; } = new();


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
                        Status = "Ready"
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
                item.Status = "In Progress";
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

                    item.Status = "Done";
                }
                catch (Exception ex)
                {
                    item.Status = "Error";
                    _growlService.ShowError($"{item.Name}: {ex.Message}");
                }
            }

            _growlService.ShowSuccess("Auto inject completed.");
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

        [RelayCommand]
        private void NavigateToBreadcrumb(string path)
        {
            FolderEditorRootPath = path;
            LoadFolderEditor();
        }

        [RelayCommand]
        private void RefreshFolder()
        {
            LoadFolderEditor();
        }

        [RelayCommand]
        private void RenameFolderItem(FolderItem item)
        {
            if (item == null) return;

            // Запрашиваем новое имя через стандартное InputBox
            string prompt = "Введите новое имя для '" + item.Name + "'";
            string title = "Переименовать";
            // Используем Microsoft.VisualBasic для InputBox
            string newName = Microsoft.VisualBasic.Interaction.InputBox(prompt, title, item.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

            var newPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName);
            try
            {
                if (item.IsDirectory)
                    Directory.Move(item.FullPath, newPath);
                else
                    File.Move(item.FullPath, newPath);

                item.Name = newName;
                item.FullPath = newPath;
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
            // удаляем из всех веток
            RemoveFromParent(FolderItems, source);

            // корректируем insertIndex
            if (insertIndex < 0) insertIndex = 0;
            if (insertIndex > destCollection.Count) insertIndex = destCollection.Count;

            // вставляем именно туда
            destCollection.Insert(insertIndex, source);
        }

        public void ReparentVisual(FolderItem source, FolderItem newParent)
        {
            // удаляет source из любой ветки FolderItems
            RemoveFromParent(FolderItems, source);
            // сразу добавляет в конец newParent.Children
            newParent.Children.Add(source);
        }

        public void UpdateBreadcrumbs()
        {
            // 1) Собираем полный путь в локальный список
            var all = new List<BreadcrumbItem>();

            if (string.IsNullOrWhiteSpace(RootFolder))
            {
                // нет корня — очищаем
                Breadcrumbs.Clear();
                Overflow.Clear();
                OnPropertyChanged(nameof(DisplayBreadcrumbs));
                OnPropertyChanged(nameof(Overflow));
                return;
            }

            // первая крошка — базовая папка
            all.Add(new BreadcrumbItem
            {
                Name = Path.GetFileName(RootFolder.TrimEnd(Path.DirectorySeparatorChar)),
                FullPath = RootFolder
            });

            // если мы «зашли» глубже — добавляем вложенные сегменты
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

            // 2) Кладём всё в вашу ObservableCollection<BreadcrumbItem>
            Breadcrumbs.Clear();
            foreach (var b in all)
                Breadcrumbs.Add(b);

            // 3) Считаем Overflow (для «…»-меню) и оповещаем дисплей
            const int MAX_VISIBLE = 5;
            if (all.Count <= MAX_VISIBLE)
            {
                Overflow.Clear();
            }
            else
            {
                // пропускаем первый и последние (MAX_VISIBLE-1)
                Overflow = all
                    .Skip(1)
                    .Take(all.Count - MAX_VISIBLE + 1)
                    .ToList();
            }

            // 4) Уведомляем об изменении DisplayBreadcrumbs и Overflow
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

    public class FolderItem : ObservableObject
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }

        // <<< Добавляем здесь >>>
        public PackIconMaterialKind IconKind { get; set; }

        public FolderItem() { }

        public ObservableCollection<FolderItem> Children { get; }
            = new ObservableCollection<FolderItem>();

        // Можно добавить конструктор для удобства:
        public FolderItem(string name, string fullPath, bool isDirectory, PackIconMaterialKind icon)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
            IconKind = icon;
        }


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
