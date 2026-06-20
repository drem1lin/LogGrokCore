using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace LogGrokCore.Localization
{
    /// <summary>
    /// XAML markup extension that produces a one-way binding to a localized string,
    /// e.g. <c>Header="{loc:Loc Menu_Open}"</c>. The binding refreshes automatically
    /// when the language changes.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension()
        {
        }

        public LocExtension(string key) => Key = key;

        /// <summary>Resource key to look up in Strings.resx.</summary>
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding($"[{Key}]")
            {
                Source = TranslationSource.Instance,
                Mode = BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}
