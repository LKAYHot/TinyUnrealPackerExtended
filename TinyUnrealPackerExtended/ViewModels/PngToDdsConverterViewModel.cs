using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.Assets.Exports.Texture;
using TinyUnrealPackerExtended.Models;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class PngToDdsConverterViewModel : ViewModelBase
    {
        private readonly IFileDialogService _fileDialogService;
        private readonly IProcessRunner _processRunner;
        private readonly IUassetInspectorService _inspector;

        [ObservableProperty] private string uassetFilePath;
        [ObservableProperty] private string uassetFormat;

        private static readonly string[] ImageExtensions = new[] { ".png", ".jpg", ".jpeg" };

        public ObservableCollection<string> FilteredFormats { get; } = new();

        public ObservableCollection<ConversionItem> ConversionItems { get; } = new();

        [ObservableProperty] private string converterOutputPath;
        [ObservableProperty] private string selectedFormat;
        [ObservableProperty] private bool isConverting;

        public bool HasConversionItems => ConversionItems.Any();
        public string ConverterOutputButtonText
            => string.IsNullOrEmpty(ConverterOutputPath)
               ? "Select output folder"
               : ConverterOutputPath;

        public string UassetButtonText
        => string.IsNullOrEmpty(UassetFilePath)
           ? "Select .uasset file"
           : Path.GetFileName(UassetFilePath);

        public PngToDdsConverterViewModel(
            IFileDialogService fileDialogService,
            GrowlService growlService,
            IProcessRunner processRunner)
            : base(fileDialogService, growlService)
        {
            _fileDialogService = fileDialogService;
            _processRunner = processRunner;
            _inspector = new UassetInspectorService();

            SelectedFormat = PngDdsFormatDefinitions.AvailableFormats.First();
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UassetFilePath))
                {
                    OnPropertyChanged(nameof(UassetButtonText));
                    LoadUassetFormat();
                }
                if (e.PropertyName == nameof(ConverterOutputPath))
                    OnPropertyChanged(nameof(ConverterOutputButtonText));
            };
            ConversionItems.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasConversionItems));

            foreach (var fmt in PngDdsFormatDefinitions.AvailableFormats)
                FilteredFormats.Add(fmt);
            SelectedFormat = FilteredFormats.First();
        }

        [RelayCommand]
        private async Task BrowseUassetFileAsync()
        {
            var path = await _fileDialogService.PickFileAsync(
                filter: "Unreal asset|*.uasset",
                title: "Select a UAsset");
            if (string.IsNullOrEmpty(path)) return;
            UassetFilePath = path;
        }

        private async void LoadUassetFormat()
        {
            if (string.IsNullOrEmpty(UassetFilePath))
            {
                UassetFormat = null;
                UpdateFormatFilter(null);
                return;
            }

            try
            {
                var (pf, mapped) = await _inspector.InspectAsync(UassetFilePath, CancellationToken.None);
                UassetFormat = $"{pf} → {mapped}";
                UpdateFormatFilter(mapped);
            }
            catch (Exception ex)
            {
                UassetFormat = $"Error: {ex.Message}";
                UpdateFormatFilter(null);
            }
        }

        private void UpdateFormatFilter(string? mappedCsv)
        {
            FilteredFormats.Clear();
            var list = mappedCsv?
                .Split(',')
                .Select(s => s.Trim())
                .Where(PngDdsFormatDefinitions.AvailableFormats.Contains)
                .ToList()
                ?? PngDdsFormatDefinitions.AvailableFormats.ToList();

            foreach (var fmt in list)
                FilteredFormats.Add(fmt);

            SelectedFormat = list.FirstOrDefault();
        }


        public void LoadFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;

            foreach (var path in paths)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (!ImageExtensions.Contains(ext)) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                if (ConversionItems.Any(i => i.Name == name)) continue;

                ConversionItems.Add(new ConversionItem
                {
                    Name = name,
                    FilePath = path,
                    Status = "Loaded"
                });
            }
        }

        [RelayCommand]
        private async Task BrowseFilesAsync()
        {
            var paths = await _fileDialogService.PickFilesAsync(
                filter: "Image files|*.png;*.jpg;*.jpeg",
                title: "Select image files");
            LoadFiles(paths);
        }

        [RelayCommand]
        private async Task BrowseOutputFolderAsync()
        {
            var folder = await _fileDialogService.PickFolderAsync(
                description: "Выбрать папку");
            if (string.IsNullOrEmpty(folder)) return;
            ConverterOutputPath = folder;
        }

        [RelayCommand]
        private async Task ConvertAsync()
        {
            if (!HasConversionItems)
            {
                ShowWarning("Нет файлов для конвертации");
                return;
            }

            await ExecuteWithBusyFlagAsync(async ct =>
            {
                foreach (var item in ConversionItems)
                {
                    item.Status = "Конвертация...";
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ConvertPngToDds.bat"),
                            Arguments = $"\"{item.FilePath}\" \"{ConverterOutputPath}\" {SelectedFormat}",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        var proc = Process.Start(psi);
                        await proc.WaitForExitAsync(ct);

                        if (proc.ExitCode != 0)
                            throw new InvalidOperationException($"texconv вернулся с кодом {proc.ExitCode}");

                        item.Status = "Готово";
                    }
                    catch (Exception ex)
                    {
                        item.Status = "Ошибка";
                        ShowError($"{item.Name}: {ex.Message}");
                    }
                }

                ShowSuccess($"Конвертор завершил работу. Файлы сохранились по пути:\n{ConverterOutputPath}");
            },
            setBusy: b => IsConverting = b,
            cancellationToken: CancellationToken.None);
        }

        [RelayCommand]
        private void RemoveItem(ConversionItem item)
        {
            if (item != null)
                ConversionItems.Remove(item);
        }

        [RelayCommand]
        private void DropFiles(string[] paths)
        {
            LoadFiles(paths);
        }
    }

    public partial class ConversionItem : ObservableObject
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        [ObservableProperty] private string status;
    }
}
