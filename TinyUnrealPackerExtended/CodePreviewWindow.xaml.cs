using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using DocumentFormat.OpenXml.Bibliography;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using TinyUnrealPackerExtended.Properties;

namespace TinyUnrealPackerExtended
{
   
    public partial class CodePreviewWindow : Window
    {
        public CodePreviewWindow()
        {
            InitializeComponent();

            string theme = Settings.Default.AppTheme;
            string resourceName = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? "TinyUnrealPackerExtended.Resources.JsonDark.xshd"
                : "TinyUnrealPackerExtended.Resources.Json.xshd";

            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                MessageBox.Show($"Не удалось найти ресурс '{resourceName}'", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using var reader = new XmlTextReader(stream);
            EditorMain.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DragArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
