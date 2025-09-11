using System.Configuration;
using System.Data;
using System.Windows;
using TinyUnrealPackerExtended.Helpers;
using TinyUnrealPackerExtended.Properties;

namespace TinyUnrealPackerExtended
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            var merged = Resources.MergedDictionaries;

            var themeName = Settings.Default.AppTheme;
            var themeDict = new ResourceDictionary
            {
                Source = new Uri($"Resources/Themes/{themeName}Theme.xaml", UriKind.Relative)
            };
            merged.Add(themeDict);

            var stylesDict = new ResourceDictionary
            {
                Source = new Uri("/TinyUnrealPackerExtended;component/Resources/Styles.xaml",
                                 UriKind.Relative)
            };
            merged.Add(stylesDict);

            var animDict = new ResourceDictionary
            {
                Source = new Uri("/TinyUnrealPackerExtended;component/Resources/Animations.xaml",
                                 UriKind.Relative)
            };
            merged.Add(animDict);

            base.OnStartup(e);
            LocalizationManager.ApplyOnStartup();
        }
    }

}
