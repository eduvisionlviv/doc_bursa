using System;
using System.Globalization;
using System.Windows.Data;

namespace doc_bursa.Converters
{
    /// <summary>
    /// Перетворює null у false та ненульові значення у true для прив'язок IsEnabled.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
