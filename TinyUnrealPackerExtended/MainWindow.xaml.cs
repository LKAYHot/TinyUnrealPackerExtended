using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using HandyControl.Controls;
using MahApps.Metro.IconPacks;
using TinyUnrealPackerExtended.Services;
using TinyUnrealPackerExtended.ViewModels;

namespace TinyUnrealPackerExtended
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly MainWindowViewModel mainWindowViewModel;

        public MainWindow()
        {
            InitializeComponent();
            var growlService = new GrowlService(GrowlContainer);
            var fileDialog = new FileDialogService();
            var proccessRunner = new ProcessRunner();
            var fileSystem = new FileSystemService();
            mainWindowViewModel = new MainWindowViewModel(new DialogService(), growlService, fileDialog, proccessRunner, fileSystem, this);
            DataContext = mainWindowViewModel;
        }

        private void OpenShell_Click(object sender, RoutedEventArgs e)
        {
            var shell = new ShellWindow
            {
                Owner = this
            };
            shell.Show();
        }



        private void DragArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
