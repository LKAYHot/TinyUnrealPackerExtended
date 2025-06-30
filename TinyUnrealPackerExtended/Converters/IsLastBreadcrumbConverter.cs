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
    public class IsLastBreadcrumbConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] — текущий BreadcrumbItem
            // values[1] — вся коллекция Breadcrumbs
            if (values[0] is null || values[1] is not IEnumerable<object> col)
                return Visibility.Collapsed;

            var list = col.Cast<object>().ToList();
            return !ReferenceEquals(values[0], list.LastOrDefault())
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
