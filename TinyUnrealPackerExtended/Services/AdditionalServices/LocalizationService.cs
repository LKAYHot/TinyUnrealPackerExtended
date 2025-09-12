using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.Services.AdditionalServices
{
    public class LocalizationService : ILocalizationService
    {
        public string this[string key] =>
            Application.Current.Resources[key] as string ?? key;
    }
}
