using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace TML
{
    /// <summary>
    /// Converter for <see cref="HierarchicalNumber"/> and its string representation.
    /// </summary>
    public class HierarchicalNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "";
            return ((HierarchicalNumber)value).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var strValue = value as string;

            if (strValue == null || strValue == "")
                return null;
            try
            {
                return HierarchicalNumber.ParseFromString((string)value);
            }
            catch(Exception x)
            {
                return new ValidationResult(false, x.Message);
            }
        }
    }
}
