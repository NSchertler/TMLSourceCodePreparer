using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace SourceCodePreparer
{
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
            if (value == "")
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
