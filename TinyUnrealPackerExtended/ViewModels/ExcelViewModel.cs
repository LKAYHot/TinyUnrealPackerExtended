using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using TinyUnrealPackerExtended.Services;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.ViewModels;
using LocresLib;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class ExcelViewModel : ObservableObject
    {
        private readonly ExcelService _excelService;
        private readonly GrowlService _growlService;
        private readonly IFileDialogService _fileDialogService;

        public ObservableCollection<FileItem> ExcelFiles { get; } = new();

        [ObservableProperty] private bool isExcelBusy;
        [ObservableProperty] private string excelStatusMessage;
        [ObservableProperty] private string excelOutputPath;

        public ExcelViewModel(
            ExcelService excelService,
            GrowlService growlService,
            IFileDialogService fileDialogService)
        {
            _excelService = excelService;
            _growlService = growlService;
            _fileDialogService = fileDialogService;
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

        private async Task<bool> TryPickSingleFileAsync(
            string filter,
            string title,
            ObservableCollection<FileItem> target)
        {
            var path = await _fileDialogService.PickFileAsync(filter, title);
            if (string.IsNullOrEmpty(path))
                return false;

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

        [RelayCommand]
        private void RemoveFile(FileItem file)
        {
            ExcelFiles.Remove(file);
        }
    }
}
