using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Markup;

namespace TinyUnrealPackerExtended.Helpers
{
    public static class LocalizationManager
    {
        private static bool _wpfLanguageOverridden;

        public static void ApplyOnStartup()
        {
            var code = Properties.Settings.Default.AppLanguage;
            if (string.IsNullOrWhiteSpace(code))
                code = "en";

            ApplyCulture(code);
            ApplyLocalizationDictionary(code);
        }

        private static void ApplyCulture(string code)
        {
            var culture = new CultureInfo(code);

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            if (!_wpfLanguageOverridden)
            {
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
                _wpfLanguageOverridden = true;
            }
        }

        private static void ApplyLocalizationDictionary(string code)
        {
            var app = Application.Current;
            if (app == null) return;

            var dicts = app.Resources.MergedDictionaries;

            var toRemove = dicts
                .Where(d => d.Source != null &&
                            d.Source.OriginalString.IndexOf("Resources/Localization/Strings.", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            foreach (var d in toRemove)
                dicts.Remove(d);

            var langTag = code.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            var newDict = new ResourceDictionary
            {
                Source = new Uri($"/TinyUnrealPackerExtended;component/Resources/Localization/Strings.{langTag}.xaml", UriKind.Relative)
            };
            dicts.Add(newDict);
        }
    }
}
