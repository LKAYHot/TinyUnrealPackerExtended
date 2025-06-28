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

        public ObservableCollection<FileItem> ExcelFiles { get; } = new();

        [ObservableProperty] private bool isExcelBusy;
        [ObservableProperty] private string excelStatusMessage;
        [ObservableProperty] private string excelOutputPath;

        public ExcelViewModel(
            ExcelService excelService,
            GrowlService growlService,
            IFileDialogService fileDialogService)
            : base(fileDialogService, growlService)
        {
            _excelService = excelService;
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
                    ExcelStatusMessage = $"Импорт из Excel завершён: {output}";
                }
                else if (ext == ".csv")
                {
                    output = Path.ChangeExtension(input, ".xlsx");
                    await Task.Run(() => _excelService.ExportToExcel(input, output), ct);
                    ExcelStatusMessage = $"Экспорт в Excel завершён: {output}";
                }
                else
                {
                    ExcelStatusMessage = "Неподдерживаемый формат. Поддерживаются .xlsx и .csv.";
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
    }
}
