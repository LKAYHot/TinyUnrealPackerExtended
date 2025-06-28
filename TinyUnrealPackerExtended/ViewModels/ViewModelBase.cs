using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;

namespace TinyUnrealPackerExtended.ViewModels
{
    /// <summary>
    /// Базовый класс для ViewModel, предоставляющий общую логику работы с диалогами файлов и флагом занятости.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
        protected readonly IFileDialogService _fileDialogService;
        protected readonly GrowlService _growlService;

        protected ViewModelBase(IFileDialogService fileDialogService, GrowlService growlService)
        {
            _fileDialogService = fileDialogService;
            _growlService = growlService;
        }

     
        protected async Task<bool> TryPickSingleFileAsync(
            string filter,
            string title,
            ObservableCollection<FileItem> target)
        {
            var path = await _fileDialogService.PickFileAsync(filter, title);
            if (string.IsNullOrEmpty(path)) return false;

            target.Clear();
            target.Add(new FileItem
            {
                FileName = System.IO.Path.GetFileName(path),
                FilePath = path
            });
            return true;
        }

        /// <summary>
        /// Выполняет асинхронную операцию с установкой флага занятости и обработкой ошибок.
        /// </summary>
        protected async Task ExecuteWithBusyFlagAsync(
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

        protected void ShowSuccess(string message) => _growlService.ShowSuccess(message);
        protected void ShowWarning(string message) => _growlService.ShowWarning(message);
        protected void ShowError(string message) => _growlService.ShowError(message);
    }
}
