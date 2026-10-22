using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MiHAva.UI.ViewModels.Spec;

namespace MiHAva.UI.Converters;

public sealed class SpecKindToBooleanConverter : IValueConverter
{
    public SpecPropertyKind TargetKind { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SpecPropertyKind kind)
        {
            return kind == TargetKind;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
