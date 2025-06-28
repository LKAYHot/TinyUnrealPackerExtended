using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class UassetInjectorViewModel : ViewModelBase
    {
        private readonly IProcessRunner _processRunner;

        public ObservableCollection<FileItem> InjectFiles { get; } = new();
        public ObservableCollection<FileItem> TextureFiles { get; } = new();

        [ObservableProperty] private string injectOutputPath;
        [ObservableProperty] private bool isInjectBusy;
        [ObservableProperty] private string injectStatusMessage;

        /// <summary>
        /// Текст кнопки вывода: либо подсказка, либо выбранный путь
        /// </summary>
        public string InjectOutputButtonText
            => string.IsNullOrEmpty(InjectOutputPath)
               ? "Выберите конечный путь"
               : InjectOutputPath;

        public bool HasInjectFile => InjectFiles.Any();
        public bool HasTextureFiles => TextureFiles.Any();

        public UassetInjectorViewModel(
            IFileDialogService fileDialogService,
            GrowlService growlService,
            IProcessRunner processRunner)
            : base(fileDialogService, growlService)
        {
            _processRunner = processRunner;

            // обновление текста кнопки при смене пути
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(InjectOutputPath))
                    OnPropertyChanged(nameof(InjectOutputButtonText));
            };

            InjectFiles.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasInjectFile));
            TextureFiles.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasTextureFiles));
        }

        [RelayCommand]
        private async Task BrowseOriginalUassetAsync()
        {
            var path = await _fileDialogService.PickFileAsync(
                filter: "UAsset|*.uasset",
                title: "Выберите .uasset файл");
            if (string.IsNullOrEmpty(path))
                return;

            InjectFiles.Clear();
            InjectFiles.Add(new FileItem
            {
                FileName = Path.GetFileName(path),
                FilePath = path,
                IconKind = PackIconMaterialKind.FileDocumentOutline
            });
        }

        [RelayCommand]
        private async Task BrowseTextureAsync()
        {
            var paths = await _fileDialogService.PickFilesAsync(
                filter: "Image|*.png;*.jpg;*.tga;*.dds",
                title: "Выберите одну или несколько текстур");
            if (paths == null || paths.Length == 0)
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
            if (!HasInjectFile || !HasTextureFiles || string.IsNullOrEmpty(InjectOutputPath))
            {
                InjectStatusMessage = "Укажите .uasset, текстуры и папку вывода.";
                ShowWarning(InjectStatusMessage);
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

                // записываем путь к исходному файлу
                await File.WriteAllTextAsync(
                    Path.Combine(ddsDir, "src", "_file_path_.txt"),
                    assetPath,
                    ct);

                // запускаем батч с передачей путей текстур
                var args = string.Join(" ", TextureFiles.Select(f => $"\"{f.FilePath}\""));
                var exitCode = await _processRunner.RunAsync(
                    exePath: Path.Combine(ddsDir, "_3_inject.bat"),
                    arguments: args,
                    workingDirectory: ddsDir,
                    cancellationToken: ct);

                if (exitCode != 0)
                    throw new InvalidOperationException($"Batch вернулся с кодом {exitCode}");

                // копируем результаты
                foreach (var file in Directory.GetFiles(injected))
                {
                    File.Copy(file,
                              Path.Combine(InjectOutputPath, Path.GetFileName(file)!),
                              overwrite: true);
                }

                InjectStatusMessage = "Инжект окончен.";
                ShowSuccess(InjectStatusMessage);

            },
            setBusy: b => IsInjectBusy = b,
            cancellationToken: token);
        }

        [RelayCommand]
        private void CancelInject()
        {
            InjectFiles.Clear();
            TextureFiles.Clear();
            InjectOutputPath = string.Empty;
            IsInjectBusy = false;
            InjectStatusMessage = string.Empty;
        }

        [RelayCommand]
        private void RemoveFile(FileItem file)
        {
            InjectFiles.Remove(file);
            TextureFiles.Remove(file);
        }
    }
}
