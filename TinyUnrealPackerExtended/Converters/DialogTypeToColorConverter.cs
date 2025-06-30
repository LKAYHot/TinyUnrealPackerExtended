using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.Converters
{
    public class DialogTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is DialogType type ? type switch
            {
                DialogType.Error => Brushes.IndianRed,
                DialogType.Info => Brushes.DodgerBlue,
                DialogType.Confirm => Brushes.Gray,
                _ => Brushes.Gray
            } : Brushes.Gray;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
