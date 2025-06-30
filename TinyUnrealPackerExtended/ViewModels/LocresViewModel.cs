using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class LocresViewModel : ViewModelBase
    {
        private readonly LocresService _locresService;

        public ObservableCollection<FileItem> LocresFiles { get; } = new();
        public ObservableCollection<FileItem> OriginalLocresFiles { get; } = new();

        [ObservableProperty] private bool isLocresBusy;
        [ObservableProperty] private bool isCsvFileDropped;
        [ObservableProperty] private bool isLocresFileDropped;
        [ObservableProperty] private string locresStatusMessage;
        [ObservableProperty] private string locresOutputPath;

        public LocresViewModel(
            LocresService locresService,
            GrowlService growlService,
            IFileDialogService fileDialogService)
            : base(fileDialogService, growlService)
        {
            _locresService = locresService;

            LocresFiles.CollectionChanged += OnLocresCollectionsChanged;
            OriginalLocresFiles.CollectionChanged += OnLocresCollectionsChanged;
        }

        private void OnLocresCollectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            IsLocresFileDropped = LocresFiles.Count > 0
                               || OriginalLocresFiles.Count > 0;
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
                IsCsvFileDropped = (ext == ".csv");
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
                ShowWarning(LocresStatusMessage);
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
                ShowSuccess(LocresStatusMessage);
            },
            setBusy: b => IsLocresBusy = b,
            cancellationToken: token);
        }

        [RelayCommand]
        private void CancelLocres()
        {
            LocresFiles.Clear();
            OriginalLocresFiles.Clear();
            IsCsvFileDropped = false;
            IsLocresFileDropped = false;
            IsLocresBusy = false;
            LocresStatusMessage = string.Empty;
            LocresOutputPath = string.Empty;
        }

        [RelayCommand]
        private void RemoveFile(FileItem file)
        {
            LocresFiles.Remove(file);
            OriginalLocresFiles.Remove(file);
        }

        [RelayCommand]
        private void DropLocresFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (LocresFiles.Count > 0) return; // можно только один

            foreach (var path in paths)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".csv" || ext == ".locres")
                {
                    // если CSV — чистим Original и ставим флаг
                    if (ext == ".csv")
                    {
                        IsCsvFileDropped = true;
                        OriginalLocresFiles.Clear();
                    }
                    else // .locres
                    {
                        IsCsvFileDropped = false;
                    }

                    LocresFiles.Add(new FileItem
                    {
                        FileName = Path.GetFileName(path),
                        FilePath = path
                    });
                    break; // только первый подходящий
                }
            }
        }

        [RelayCommand]
        private void DropOriginalLocresFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (OriginalLocresFiles.Count > 0) return;

            foreach (var path in paths)
            {
                if (Path.GetExtension(path).Equals(".locres", StringComparison.OrdinalIgnoreCase))
                {
                    OriginalLocresFiles.Add(new FileItem
                    {
                        FileName = Path.GetFileName(path),
                        FilePath = path
                    });
                    IsCsvFileDropped = false;
                    break;
                }
            }
        }
    }
}
