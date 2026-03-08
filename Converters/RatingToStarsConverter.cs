using System;
using System.Globalization;
using System.Windows.Data;

namespace AssetVault.Converters
{
    public class RatingToStarsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int rating = 0;

            if (value is int intValue)
                rating = intValue;
            else if (value != null)
                int.TryParse(value.ToString(), out rating);

            if (rating < 0) rating = 0;
            if (rating > 5) rating = 5;

            return new string('★', rating) + new string('☆', 5 - rating);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 0;
        }
    }
}