using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace LogGrokCore.Controls
{
    /// <summary>
    /// Multi-value converter for headers whose format string is itself localized and so
    /// must be supplied through a binding (ConverterParameter is not bindable).
    /// Usage: a MultiBinding with [0] = argument, [1] = format string ("Search '{0}'").
    /// </summary>
    public class FormatTextMultiExtension : MarkupExtension, IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is string text && values[1] is string format)
                return string.Format(culture, format, text);
            return values.Length > 0 ? values[0] : string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}
