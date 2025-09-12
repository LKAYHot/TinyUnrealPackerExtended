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
        private readonly ILocalizationService _loc;

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
            IFileDialogService fileDialogService,
            ILocalizationService localizationService)
            : base(fileDialogService, growlService)
        {
            _locresService = locresService;

            LocresFiles.CollectionChanged += OnLocresCollectionsChanged;
            OriginalLocresFiles.CollectionChanged += OnLocresCollectionsChanged;

            _loc = localizationService;
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
                LocresStatusMessage = _loc["Locres.BrowseInput.OnlyOne"];
                return;
            }

            if (await TryPickSingleFileAsync(
                    filter: _loc["Locres.BrowseInput.Filter"],    
                    title: _loc["Locres.BrowseInput.Title"],      
                    target: LocresFiles))
            {
                var ext = Path.GetExtension(LocresFiles.First().FilePath).ToLowerInvariant();
                IsCsvFileDropped = (ext == ".csv");
            }
        }

        [RelayCommand]
        private async Task BrowseOriginalLocresAsync()
        {
            if (OriginalLocresFiles.Count >= 1)
            {
                LocresStatusMessage = _loc["Locres.BrowseOriginal.OnlyOne"];
                return;
            }

            if (await TryPickSingleFileAsync(
                    filter: _loc["Locres.BrowseOriginal.Filter"],    
                    title: _loc["Locres.BrowseOriginal.Title"],    
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
                LocresStatusMessage = _loc["Locres.Error.NoInput"];
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
                    LocresStatusMessage = string.Format(_loc["Locres.Export.Done"], output);
                }
                else
                {
                    output = OriginalLocresFiles.First().FilePath;
                    await Task.Run(() => _locresService.Import(input, output), ct);
                    LocresStatusMessage = string.Format(_loc["Locres.Import.Done"], output);
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
            if (LocresFiles.Count > 0) return; 

            foreach (var path in paths)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".csv" || ext == ".locres")
                {
                    if (ext == ".csv")
                    {
                        IsCsvFileDropped = true;
                        OriginalLocresFiles.Clear();
                    }
                    else
                    {
                        IsCsvFileDropped = false;
                    }

                    LocresFiles.Add(new FileItem
                    {
                        FileName = Path.GetFileName(path),
                        FilePath = path
                    });
                    break; 
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
