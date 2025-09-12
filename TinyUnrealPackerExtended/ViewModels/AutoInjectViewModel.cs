using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class AutoInjectViewModel : ViewModelBase
    {
        private readonly IProcessRunner _processRunner;
        private readonly ILocalizationService _loc;

        private static readonly string[] TextureExtensions = new[] { ".png", ".jpg", ".jpeg", ".tga", ".dds" };



        public ObservableCollection<AutoInjectItem> AutoInjectItems { get; } = new();

        [ObservableProperty] private string autoInjectOutputPath;
        [ObservableProperty] private bool isAutoInjectBusy;

        public bool HasAutoInjectItems => AutoInjectItems.Any();

        public string AutoInjectOutputButtonText
            => string.IsNullOrEmpty(AutoInjectOutputPath)
               ? _loc["AutoInject.SelectOutput"]
               : AutoInjectOutputPath;

        public AutoInjectViewModel(
            IFileDialogService fileDialogService,
            GrowlService growlService,
            IProcessRunner processRunner, ILocalizationService localization)
            : base(fileDialogService, growlService)
        {
            _processRunner = processRunner;

            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AutoInjectOutputPath))
                    OnPropertyChanged(nameof(AutoInjectOutputButtonText));
            };

            AutoInjectItems.CollectionChanged += (_, __)
                => OnPropertyChanged(nameof(HasAutoInjectItems));

            _loc = localization;
        }

        public void LoadAutoFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;

            var groups = paths
                .Select(p => new { Path = p, Base = System.IO.Path.GetFileNameWithoutExtension(p), Ext = System.IO.Path.GetExtension(p).ToLowerInvariant() })
                .GroupBy(x => x.Base);

            foreach (var g in groups)
            {
                if (AutoInjectItems.Any(item => item.Name == g.Key)) continue;

                var asset = g.FirstOrDefault(x => x.Ext == ".uasset");
                var tex = g.FirstOrDefault(x => TextureExtensions.Contains(x.Ext));

                if (asset != null && tex != null)
                {
                    AutoInjectItems.Add(new AutoInjectItem
                    {
                        Name = g.Key,
                        AssetFile = new FileItem { FileName = System.IO.Path.GetFileName(asset.Path), FilePath = asset.Path },
                        TextureFile = new FileItem { FileName = System.IO.Path.GetFileName(tex.Path), FilePath = tex.Path },
                        Status = _loc["AutoInject.Loaded"]
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
            LoadAutoFiles(paths);
        }

        [RelayCommand]
        private async Task BrowseAutoOutputAsync()
        {
            var folder = await _fileDialogService.PickFolderAsync(
                description: "Выберите папку вывода");
            if (string.IsNullOrEmpty(folder)) return;
            AutoInjectOutputPath = folder;
        }

        [RelayCommand]
        private async Task ProcessAutoInjectAsync()
        {
            if (!HasAutoInjectItems)
            {
                ShowWarning(_loc["AutoInject.NoPairs"]);
                return;
            }

            await ExecuteWithBusyFlagAsync(async ct =>
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var ddsDir = Path.Combine(baseDir, "DDS");
                var injectedDir = Path.Combine(ddsDir, "injected");

                if (Directory.Exists(injectedDir))
                    Directory.Delete(injectedDir, true);
                Directory.CreateDirectory(injectedDir);

                foreach (var item in AutoInjectItems)
                {
                    item.Status = _loc["AutoInject.InProgress"];
                    try
                    {
                        File.WriteAllText(
                            Path.Combine(ddsDir, "src", "_file_path_.txt"),
                            item.AssetFile.FilePath);

                        var args = $"\"{item.TextureFile.FilePath}\"";
                        var exitCode = await _processRunner.RunAsync(
                            exePath: Path.Combine(ddsDir, "_3_inject.bat"),
                            arguments: args,
                            workingDirectory: ddsDir,
                            cancellationToken: ct);

                        if (exitCode != 0)
                            throw new InvalidOperationException($"Batch get {exitCode}");

                        if (!string.IsNullOrEmpty(AutoInjectOutputPath))
                        {
                            foreach (var file in Directory.GetFiles(injectedDir))
                                File.Copy(file, Path.Combine(AutoInjectOutputPath, Path.GetFileName(file)!), true);
                        }

                        item.Status = _loc["AutoInject.Done"];
                    }
                    catch (Exception ex)
                    {
                        item.Status = _loc["AutoInject.Error"];
                        ShowError($"{item.Name}: {ex.Message}");
                    }
                }

                ShowSuccess(_loc["AutoInject.Completed"]);
            },
            setBusy: b => IsAutoInjectBusy = b,
            cancellationToken: CancellationToken.None);
        }

        [RelayCommand]
        private void RemoveAutoInjectItem(AutoInjectItem item)
        {
            if (item != null)
                AutoInjectItems.Remove(item);
        }

        [RelayCommand]
        private void CancelAutoInject()
        {
            AutoInjectItems.Clear();
            AutoInjectOutputPath = string.Empty;
            IsAutoInjectBusy = false;
        }

        [RelayCommand]
        private void DropAutoFiles(string[] paths)
        {
            LoadAutoFiles(paths);
        }
    }
}
