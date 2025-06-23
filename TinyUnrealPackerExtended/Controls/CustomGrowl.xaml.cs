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
using System.Windows.Navigation;
using System.Windows.Shapes;
using MahApps.Metro.IconPacks;

namespace TinyUnrealPackerExtended.Controls
{
    /// <summary>
    /// Логика взаимодействия для CustomGrowl.xaml
    /// </summary>
    public partial class CustomGrowl : UserControl
    {
        public CustomGrowl()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string Title { get; set; } = "Notification";
        public string Message { get; set; } = "Default message";
        public Brush IconColor { get; set; } = Brushes.LightGray;
        public PackIconMaterialKind IconKind { get; set; } = PackIconMaterialKind.InformationBoxOutline;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
