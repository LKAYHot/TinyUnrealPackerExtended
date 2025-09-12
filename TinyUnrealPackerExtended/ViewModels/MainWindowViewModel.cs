using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Services;
using TinyUnrealPackerExtended.Helpers;
using System.Windows;
using TinyUnrealPackerExtended.Services.AdditionalServices;

namespace TinyUnrealPackerExtended.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly GrowlService _growlService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IProcessRunner _processRunner;
        private readonly IFileSystemService _fileSystemService;
        private readonly IWindowActions _windowActions;


        private readonly Window _window;


        public LocresViewModel LocresVM { get; }
        public ExcelViewModel ExcelVM { get; }
        public PakViewModel PakVM { get; }
        public UassetInjectorViewModel UassetInjectorVM { get; }
        public AutoInjectViewModel AutoInjectVM { get; }

        public FolderEditorViewModel FolderEditorVM { get; }

        public PngToDdsConverterViewModel PngToDdsConverterVM { get; }



        private readonly LocresService _locresService = new();
        private readonly ExcelService _excelService = new();

        private readonly LocalizationService localizationService;



        private readonly IDialogService _dialog;



        public MainWindowViewModel(IDialogService dialogService, GrowlService growlService, IFileDialogService fileDialogService,
            IProcessRunner processRunner, IFileSystemService fileSystemService, IWindowActions windowActions, Window window)
        {
            _growlService = growlService;
            _dialog = dialogService;
            _fileDialogService = fileDialogService;
            _processRunner = processRunner;
            _fileSystemService = fileSystemService;
            _windowActions = windowActions;
            _window = window;
            localizationService = new LocalizationService();
            LocresVM = new LocresViewModel(_locresService, growlService, fileDialogService, localizationService);
            ExcelVM = new ExcelViewModel(_excelService, growlService, fileDialogService, localizationService);
            PakVM = new PakViewModel(_fileDialogService, growlService, _processRunner, localizationService);
            UassetInjectorVM = new UassetInjectorViewModel(_fileDialogService, growlService, _processRunner);
            AutoInjectVM = new AutoInjectViewModel(_fileDialogService, growlService, _processRunner, localizationService);
            FolderEditorVM = new FolderEditorViewModel(growlService, fileDialogService, dialogService, localizationService);
            PngToDdsConverterVM = new PngToDdsConverterViewModel(_fileDialogService, growlService, _processRunner, localizationService);

            PakVM.PakFiles.CollectionChanged += (_, __) =>
            {
                FolderEditorVM.CanEditFolderEditor = PakVM.PakFiles.Any();

                if (PakVM.PakFiles.Count > 0)
                {
                    var folder = PakVM.PakFiles.First().FilePath;

                    FolderEditorVM.RootFolder = folder;
                    FolderEditorVM.FolderEditorRootPath = folder;

                    FolderEditorVM.LoadFolderEditorCommand.Execute(null);
                }
            };



        }

        [RelayCommand]
        private void OpenShell()
        {
            var shell = new ShellWindow
            {
                Owner = _window
            };
            shell.Show();
        }

        [RelayCommand] 
        private void MaximizeWindow()
        {
            _windowActions.ToggleMaximizeRestore();
        }

        [RelayCommand] 
        private void MinimizeWindow()
        {
            _windowActions.Minimize();
        }

        [RelayCommand]
        private void CloseWindow()
        {
            _windowActions.Close();
        }


    }

    public class FileItem
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public PackIconMaterialKind IconKind { get; set; } = PackIconMaterialKind.FileDocumentOutline;
    }

    public partial class FolderItem : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private string fullPath;
        public bool IsDirectory { get; set; }
        public PackIconMaterialKind IconKind { get; set; }

        public ObservableCollection<FolderItem> Children { get; }
            = new ObservableCollection<FolderItem>();

        public FolderItem() { }

        public FolderItem(string name, string fullPath, bool isDirectory, PackIconMaterialKind icon)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
            IconKind = icon;
        }

        public string DateModified
            => File.GetLastWriteTime(FullPath).ToString("g");

        public string Size
            => !IsDirectory
               ? $"{new FileInfo(FullPath).Length / 1024:n0} KB"
               : string.Empty;
    }

    public partial class AutoInjectItem : ObservableObject
    {
        public string Name { get; set; }
        public FileItem AssetFile { get; set; }
        public FileItem TextureFile { get; set; }

        [ObservableProperty]
        private string status;
    }

    public class BreadcrumbItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }

        public bool IsOverflow { get; set; } = false;
    }
}
