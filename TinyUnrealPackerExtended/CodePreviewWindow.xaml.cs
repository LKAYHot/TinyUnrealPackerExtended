using DocumentFormat.OpenXml.Bibliography;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Xml;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TinyUnrealPackerExtended
{
    /// <summary>
    /// Логика взаимодействия для CodePreviewWindow.xaml
    /// </summary>
    public partial class CodePreviewWindow : Window
    {
        public CodePreviewWindow()
        {
            InitializeComponent();

            using var stream = Assembly.GetExecutingAssembly()
        .GetManifestResourceStream("TinyUnrealPackerExtended.Resources.Json.xshd");
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
