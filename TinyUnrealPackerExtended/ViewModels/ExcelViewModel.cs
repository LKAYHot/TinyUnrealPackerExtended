using System;
using System.Collections.ObjectModel;
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
    public partial class ExcelViewModel : ViewModelBase
    {
        private readonly ExcelService _excelService;
        private readonly ILocalizationService _loc;

        public ObservableCollection<FileItem> ExcelFiles { get; } = new();

        [ObservableProperty] private bool isExcelBusy;
        [ObservableProperty] private string excelStatusMessage;
        [ObservableProperty] private string excelOutputPath;

        public ExcelViewModel(
            ExcelService excelService,
            GrowlService growlService,
            IFileDialogService fileDialogService,
            ILocalizationService localizationService)
            : base(fileDialogService, growlService)
        {
            _excelService = excelService;
            _loc = localizationService;
        }

        [RelayCommand]
        private async Task BrowseExcelInputAsync()
        {
            if (ExcelFiles.Count >= 1)
            {
                ExcelStatusMessage = _loc["Excel.BrowseInput.OnlyOne"];
                return;
            }

            await TryPickSingleFileAsync(
                filter: _loc["Excel.BrowseInput.Filter"], 
                title: _loc["Excel.BrowseInput.Title"], 
                target: ExcelFiles);
        }

        [RelayCommand]
        private Task ProcessExcelAsync(CancellationToken token)
        {
            if (ExcelFiles.Count == 0)
            {
                ExcelStatusMessage = _loc["Excel.Error.NoInput"];
                ShowWarning(ExcelStatusMessage);
                return Task.CompletedTask;
            }

            return ExecuteWithBusyFlagAsync(async ct =>
            {
                var input = ExcelFiles.First().FilePath;
                var ext = Path.GetExtension(input).ToLowerInvariant();
                string output;

                if (ext == ".xlsx")
                {
                    output = Path.ChangeExtension(input, ".csv");
                    await Task.Run(() => _excelService.ImportFromExcel(input, output), ct);
                    ExcelStatusMessage = string.Format(_loc["Excel.ImportFromExcel.Done"], output);
                }
                else if (ext == ".csv")
                {
                    output = Path.ChangeExtension(input, ".xlsx");
                    await Task.Run(() => _excelService.ExportToExcel(input, output), ct);
                    ExcelStatusMessage = string.Format(_loc["Excel.ExportToExcel.Done"], output);
                }
                else
                {
                    ExcelStatusMessage = _loc["Excel.Error.UnsupportedFormat"];
                    ShowWarning(ExcelStatusMessage);
                    return;
                }

                ExcelOutputPath = output;
                ShowSuccess(ExcelStatusMessage);
            },
            setBusy: b => IsExcelBusy = b,
            cancellationToken: token);
        }

        [RelayCommand]
        private void CancelExcel()
        {
            ExcelFiles.Clear();
            IsExcelBusy = false;
            ExcelStatusMessage = string.Empty;
            ExcelOutputPath = string.Empty;
        }

        [RelayCommand]
        private void RemoveFile(FileItem file)
        {
            ExcelFiles.Remove(file);
        }

        [RelayCommand]
        private void DropExcelFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (ExcelFiles.Count > 0) return; // only one

            foreach (var path in paths)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".xlsx" || ext == ".csv")
                {
                    ExcelFiles.Add(new FileItem
                    {
                        FileName = Path.GetFileName(path),
                        FilePath = path,
                        IconKind = ext == ".xlsx"
                            ? PackIconMaterialKind.FileExcelOutline
                            : PackIconMaterialKind.FileDocumentOutline
                    });
                    break; // first matching only
                }
            }
        }
    }
}
