using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace TinyUnrealPackerExtended.Converters
{
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        /// <summary>Что возвращать, если строка NULL или пустая</summary>
        public Visibility NullOrEmptyVisibility { get; set; } = Visibility.Collapsed;
        /// <summary>Что возвращать, если есть непустая строка</summary>
        public Visibility NotNullOrEmptyVisibility { get; set; } = Visibility.Visible;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            return string.IsNullOrEmpty(s) ? NullOrEmptyVisibility : NotNullOrEmptyVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
