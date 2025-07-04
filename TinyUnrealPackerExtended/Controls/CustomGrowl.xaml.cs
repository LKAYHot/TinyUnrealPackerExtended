// CustomGrowl.xaml.cs
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MahApps.Metro.IconPacks;

namespace TinyUnrealPackerExtended.Controls
{
    public partial class CustomGrowl : UserControl
    {
        // DependencyProperty для заголовка
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(CustomGrowl),
                new PropertyMetadata("Notification"));

        // DependencyProperty для сообщения с колбэком
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(CustomGrowl),
                new PropertyMetadata(string.Empty, OnMessageChanged));

        // DependencyProperty для цвета иконки
        public static readonly DependencyProperty IconColorProperty =
            DependencyProperty.Register(
                nameof(IconColor),
                typeof(Brush),
                typeof(CustomGrowl),
                new PropertyMetadata(Brushes.LightGray));

        // DependencyProperty для вида иконки
        public static readonly DependencyProperty IconKindProperty =
            DependencyProperty.Register(
                nameof(IconKind),
                typeof(PackIconMaterialKind),
                typeof(CustomGrowl),
                new PropertyMetadata(PackIconMaterialKind.InformationBoxOutline));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public Brush IconColor
        {
            get => (Brush)GetValue(IconColorProperty);
            set => SetValue(IconColorProperty, value);
        }

        public PackIconMaterialKind IconKind
        {
            get => (PackIconMaterialKind)GetValue(IconKindProperty);
            set => SetValue(IconKindProperty, value);
        }

        public CustomGrowl()
        {
            InitializeComponent();
            DataContext = this;
        }

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CustomGrowl)d).UpdateMessageText(e.NewValue as string);
        }

        private void UpdateMessageText(string message)
        {
            MessageTextBlock.Inlines.Clear();
            if (string.IsNullOrEmpty(message)) return;

            var pathRegex = new Regex(@"[A-Za-z]:\\(?:[^\\/:*?\""<>\r\n]+\\)*[^\\/:*?\""<>\r\n]*");
            int lastIndex = 0;

            foreach (Match match in pathRegex.Matches(message))
            {
                if (match.Index > lastIndex)
                {
                    MessageTextBlock.Inlines.Add(
                        new Run(message.Substring(lastIndex, match.Index - lastIndex)));
                }

                string path = match.Value;
                var linkBrush = (Brush)new BrushConverter().ConvertFromString("#576998");
                var link = new Hyperlink(new Run(path))
                {
                    ToolTip = path,
                    TextDecorations = TextDecorations.Underline,
                    Foreground = linkBrush,
                    Cursor = Cursors.Hand
                };
                link.Click += (s, e) => OpenPath(path);
                MessageTextBlock.Inlines.Add(link);

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < message.Length)
            {
                MessageTextBlock.Inlines.Add(
                    new Run(message.Substring(lastIndex)));
            }
        }

        private void OpenPath(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", path);
                }
                else if (File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else
                {
                    MessageBox.Show($"Путь не найден: {path}", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть путь: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Фактическое удаление элемента из контейнера выполняет GrowlService
        }
    }
}
