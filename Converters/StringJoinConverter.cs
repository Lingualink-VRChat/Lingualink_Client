using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Diagnostics; // Add this

namespace lingualink_client.Converters
{
    public class StringJoinConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // **** PLACE BREAKPOINT HERE ****
            Debug.WriteLine("StringJoinConverter.Convert CALLED.");
            if (value is IEnumerable<string> lines)
            {
                Debug.WriteLine($"StringJoinConverter: Received {lines.Count()} lines.");
                // foreach(var line in lines) Debug.WriteLine($"  Line: {line}"); // Optional: very verbose
                return string.Join(Environment.NewLine, lines);
            }
            Debug.WriteLine("StringJoinConverter: Value is not IEnumerable<string> or is null.");
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // Not needed for one-way display
        }
    }
}