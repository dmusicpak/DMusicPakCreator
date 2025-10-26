using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DMusicPakCreator.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool boolValue = value is bool && (bool)value;
        bool invert = parameter as string == "Invert";
        
        return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        bool invert = parameter as string == "Invert";
        bool visible = value is Visibility && (Visibility)value == Visibility.Visible;
        
        return visible ^ invert;
    }
}

public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isEmpty = string.IsNullOrEmpty(value as string);
        bool invert = parameter as string == "Invert";
        
        return (isEmpty ^ invert) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}