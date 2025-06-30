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
    public partial class PakViewModel : ViewModelBase
    {
        private readonly IProcessRunner _processRunner;

        public ObservableCollection<FileItem> PakFiles { get; } = new();

        [ObservableProperty] private bool isPakBusy;
        [ObservableProperty] private string pakStatusMessage;

        public PakViewModel(
            IFileDialogService fileDialogService,
            GrowlService growlService,
            IProcessRunner processRunner)
            : base(fileDialogService, growlService)
        {
            _processRunner = processRunner;
        }

        [RelayCommand]
        private async Task BrowsePakFolderAsync()
        {
            var folder = await _fileDialogService.PickFolderAsync(
                description: "Выберите папку для упаковки");
            if (string.IsNullOrEmpty(folder))
                return;

            PakFiles.Clear();
            PakFiles.Add(new FileItem
            {
                FileName = Path.GetFileName(folder),
                FilePath = folder,
                IconKind = PackIconMaterialKind.FolderOutline
            });
        }

        [RelayCommand]
        private Task ProcessPakAsync(CancellationToken token)
            => RunPakAsync(token, compress: false);

        [RelayCommand]
        private Task ProcessPakCompressedAsync(CancellationToken token)
            => RunPakAsync(token, compress: true);

        [RelayCommand]
        private void CancelPak()
        {
            PakFiles.Clear();
            PakStatusMessage = string.Empty;
            IsPakBusy = false;
        }

        private Task RunPakAsync(CancellationToken token, bool compress)
        {
            if (!PakFiles.Any())
            {
                PakStatusMessage = "Укажите папку для упаковки.";
                ShowWarning(PakStatusMessage);
                return Task.CompletedTask;
            }

            return ExecuteWithBusyFlagAsync(async ct =>
            {
                var folder = PakFiles.First().FilePath;
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var exeDir = Path.Combine(baseDir, "UnrealPak");
                var exePath = Path.Combine(exeDir, "UnrealPak.exe");
                var listFile = Path.Combine(exeDir, "filelist.txt");

                await File.WriteAllTextAsync(
                    listFile,
                    $"\"{folder}\\*.*\" \"..\\..\\..\\*.*\"",
                    ct);

                var pakName = Path.GetFileName(folder) + ".pak";
                var pakPath = Path.Combine(Path.GetDirectoryName(folder)!, pakName);
                var args = $"\"{pakPath}\" -create=\"{listFile}\""
                              + (compress ? " -compress" : "");

                var exitCode = await _processRunner.RunAsync(
                    exePath,
                    arguments: args,
                    workingDirectory: exeDir,
                    cancellationToken: ct);

                if (exitCode == 0)
                {
                    PakStatusMessage = compress
                        ? $"Упаковано с компрессором: {pakPath}"
                        : $"Упаковано: {pakPath}";
                    ShowSuccess(PakStatusMessage);
                }
                else
                {
                    PakStatusMessage = $"Ошибка упаковки{(compress ? " (compress)" : "")} — код {exitCode}";
                    ShowError(PakStatusMessage);
                }
            },
            setBusy: b => IsPakBusy = b,
            cancellationToken: token);
        }

        [RelayCommand]
        private void RemoveFile(FileItem file)
            => PakFiles.Remove(file);

        [RelayCommand]
        private void DropPakFolder(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;

            var folder = paths.FirstOrDefault(Directory.Exists);
            if (folder == null) return;

            PakFiles.Clear();
            PakFiles.Add(new FileItem
            {
                FileName = Path.GetFileName(folder),
                FilePath = folder,
                IconKind = PackIconMaterialKind.FolderOutline
            });
        }
    }
}
