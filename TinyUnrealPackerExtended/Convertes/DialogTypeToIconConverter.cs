using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MahApps.Metro.IconPacks;
using System.Windows.Data;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.Convertes
{
    public class DialogTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is DialogType type ? type switch
            {
                DialogType.Error => PackIconMaterialKind.AlertCircleOutline,
                DialogType.Info => PackIconMaterialKind.InformationOutline,
                DialogType.Confirm => PackIconMaterialKind.HelpCircleOutline,
                _ => PackIconMaterialKind.HelpCircleOutline
            } : PackIconMaterialKind.HelpCircleOutline;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
