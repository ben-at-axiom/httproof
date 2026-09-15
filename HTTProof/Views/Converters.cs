using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HTTProof.Models;

namespace HTTProof.Views;

public sealed class NotNullConverter : IValueConverter
{
    public static readonly NotNullConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class BoolInvertConverter : IValueConverter
{
    public static readonly BoolInvertConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b ? !b : true;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b ? !b : false;
}

public sealed class StringMatchConverter : IValueConverter
{
    public static readonly StringMatchConverter Ok         = new("ok");
    public static readonly StringMatchConverter Redir      = new("redir");
    public static readonly StringMatchConverter ClientErr  = new("client-err");
    public static readonly StringMatchConverter ServerErr  = new("server-err");
    public static readonly StringMatchConverter Error      = new("error");
    public static readonly StringMatchConverter Neutral    = new("neutral");

    private readonly string _match;
    private StringMatchConverter(string match) { _match = match; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, _match, StringComparison.Ordinal);
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public static class EqualityConverters
{
    public static readonly IValueConverter IsBasic = new FuncValueConverter<AuthKind, bool>(a => a == AuthKind.Basic);
    public static readonly IValueConverter NotNone = new FuncValueConverter<AuthKind, bool>(a => a != AuthKind.None);
    public static readonly IValueConverter BodyNotNone = new FuncValueConverter<BodyKind, bool>(b => b != BodyKind.None);
}

public sealed class AuthSecretLabelConverter : IValueConverter
{
    public static readonly AuthSecretLabelConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        AuthKind.Basic  => "Pass",
        AuthKind.Bearer => "Token",
        _ => "",
    };
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class BodyKindDisplayConverter : IValueConverter
{
    public static readonly BodyKindDisplayConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        BodyKind.None => "No body",
        BodyKind.Json => "JSON",
        BodyKind.Text => "Text",
        _ => value?.ToString() ?? "",
    };
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
