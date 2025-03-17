using System.Globalization;

namespace ZeroKnowledgeSecureChat;

public class ConditionalDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            DateTime now = DateTime.Now;
            if (dateTime.Date == now.Date)
            {
                return dateTime.ToString("t");
            }
            else
            {
                return dateTime.ToString("M");
            }
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}