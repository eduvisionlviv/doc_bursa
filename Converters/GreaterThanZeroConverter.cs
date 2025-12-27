using System;
using System.Globalization;
using System.Windows.Data;

namespace FinDesk.Converters
{
    /// <summary>
    /// Конвертер для перевірки чи значення більше нуля
    /// Використовується для кольорування транзакцій (зелений/червоний)
    /// </summary>
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return decimalValue > 0;
            
            if (value is double doubleValue)
                return doubleValue > 0;
            
            if (value is int intValue)
                return intValue > 0;
            
            if (value is long longValue)
                return longValue > 0;
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("Метод ConvertBack не підтримується для GreaterThanZeroConverter");
        }
    }
}

