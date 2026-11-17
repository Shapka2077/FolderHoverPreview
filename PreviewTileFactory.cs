using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FolderHoverPreview;

internal static class PreviewTileFactory
{
    public static ThemedButton Create(
        string label,
        PreviewThemePreferences theme,
        ImageSource icon,
        Action? onClick = null)
    {
        var body = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        body.Children.Add(new Image
        {
            Source = icon,
            Width = theme.IconSize,
            Height = theme.IconSize,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, Math.Max(2, theme.TileSpacing / 2))
        });
        var caption = new TextBlock
        {
            Text = label,
            Foreground = CreateBrush(theme.TileText),
            FontFamily = new FontFamily(theme.FontFamily),
            FontSize = theme.TileFontSize,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = Math.Max(30, theme.TileHeight - theme.IconSize - 14),
            MaxWidth = Math.Max(30, theme.TileWidth - 8)
        };
        body.Children.Add(caption);

        var normalBrush = CreateBrush(theme.TileBackground);
        var hoverBrush = CreateBrush(theme.TileHoverBackground);
        var pressedBrush = CreateBrush(theme.TilePressedBackground);
        var accentBrush = CreateBrush(theme.AccentText);
        var button = new ThemedButton
        {
            Content = body,
            Width = theme.TileWidth,
            MinHeight = theme.TileHeight,
            Margin = new Thickness(0, 0, theme.TileSpacing, theme.TileSpacing),
            Padding = new Thickness(4),
            Background = normalBrush,
            CornerRadius = new CornerRadius(theme.TileCornerRadius),
            Focusable = false,
            Cursor = System.Windows.Input.Cursors.Hand,
            Style = (Style)Application.Current.FindResource("PreviewTileButton")
        };
        button.MouseEnter += (_, _) =>
        {
            button.Background = hoverBrush;
            caption.Foreground = accentBrush;
        };
        button.MouseLeave += (_, _) =>
        {
            button.Background = normalBrush;
            caption.Foreground = CreateBrush(theme.TileText);
        };
        button.PreviewMouseDown += (_, _) => button.Background = pressedBrush;
        button.PreviewMouseUp += (_, _) => button.Background = hoverBrush;
        if (onClick is not null)
            button.Click += (_, _) => onClick();

        return button;
    }

    private static Brush CreateBrush(string color)
    {
        var brush = new SolidColorBrush(PreviewThemeConfig.ParseColor(color));
        brush.Freeze();
        return brush;
    }
}
