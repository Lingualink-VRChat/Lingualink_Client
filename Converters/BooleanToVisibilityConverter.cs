using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace lingualink_client.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check parameter to invert logic if needed
                string? param = parameter as string;
                if (!string.IsNullOrEmpty(param) && (param.Equals("invert", StringComparison.OrdinalIgnoreCase) || param.Equals("inverted", StringComparison.OrdinalIgnoreCase)))
                {
                    boolValue = !boolValue;
                }
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            // Default to Collapsed if the value is not a boolean or is null
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack is not typically used for BooleanToVisibilityConverter
            // but implementing it for completeness.
            if (value is Visibility visibilityValue)
            {
                return visibilityValue == Visibility.Visible;
            }
            return false;
        }
    }
}