using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WasteVision.Desktop.Common;

public static class UiFactory
{
    public static Brush ThemeBrush(string resourceKey)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is Brush brush)
            return brush;
        return new SolidColorBrush(Colors.Gray);
    }

    public static Border Card(UIElement content, Thickness? padding = null) => new()
    {
        Child = content,
        Padding = padding ?? new Thickness(20),
        CornerRadius = new CornerRadius(14),
        Background = ThemeBrush("CardGlassBrush"),
        BorderBrush = ThemeBrush("CardBorderBrush"),
        BorderThickness = new Thickness(1)
    };

    public static TextBlock Label(string text, double size = 14, bool bold = false, Brush? foreground = null) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        Foreground = foreground ?? ThemeBrush("TextPrimaryBrush"),
        TextWrapping = TextWrapping.Wrap
    };

    public static StackPanel Stack(double spacing = 12) => new()
    {
        Spacing = spacing,
        FlowDirection = FlowDirection.RightToLeft
    };

    public static Button Action(string text, RoutedEventHandler click, bool isPrimary = true)
    {
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16, 10, 16, 10),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13
        };
        if (isPrimary)
        {
            button.Background = (Brush)Application.Current.Resources["WasteGradientMintCyan"];
            button.Foreground = new SolidColorBrush(Colors.White);
        }
        else
        {
            button.Background = ThemeBrush("TileSoftBackgroundBrush");
            button.Foreground = ThemeBrush("WasteMintBrush");
            button.BorderBrush = ThemeBrush("CardBorderBrush");
            button.BorderThickness = new Thickness(1);
        }
        button.Click += click;
        return button;
    }

    public static string ParseGlyph(string g)
    {
        if (string.IsNullOrEmpty(g)) return "";
        if (g.StartsWith("&#x", StringComparison.OrdinalIgnoreCase) && g.EndsWith(";"))
        {
            var hex = g.Substring(3, g.Length - 4);
            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
            {
                return char.ConvertFromUtf32(code);
            }
        }
        return g;
    }

    public static Border KpiCard(string title, string value, string note, string glyph, Brush iconBrush)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } } };

        var fontIcon = new FontIcon
        {
            Glyph = ParseGlyph(glyph),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 20,
            Foreground = iconBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconBox = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = ThemeBrush("TileSoftBackgroundBrush"),
            BorderBrush = ThemeBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 0),
            Child = fontIcon
        };
        Grid.SetColumn(iconBox, 0);
        grid.Children.Add(iconBox);

        var textStack = new StackPanel { Spacing = 3 };
        textStack.Children.Add(new TextBlock { Text = title, FontSize = 11, Foreground = ThemeBrush("TextSecondaryBrush") });
        textStack.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = ThemeBrush("TextPrimaryBrush") });
        textStack.Children.Add(new TextBlock { Text = note, FontSize = 10, Foreground = ThemeBrush("TextSecondaryBrush") });
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(12),
            Background = ThemeBrush("CardGlassBrush"),
            BorderBrush = ThemeBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            MinWidth = 170
        };
    }

    public static void SetKpiValue(Border kpi, string text)
    {
        if (kpi.Child is Grid grid && grid.Children[1] is StackPanel stack && stack.Children[1] is TextBlock valText)
        {
            valText.Text = text;
        }
    }
}
