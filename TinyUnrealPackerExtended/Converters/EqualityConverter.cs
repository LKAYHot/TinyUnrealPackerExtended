using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TinyUnrealPackerExtended.Converters
{
    public class EqualityConverter : IMultiValueConverter
    {
        // value = SelectedTheme, parameter = this button’s theme
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 &&
                values[0] is string sel &&
                values[1] is string item)
            {
                return sel == item;
            }
            return false;
        }

        // two-way not needed: we handle clicks with a command
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
