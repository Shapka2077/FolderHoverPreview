using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace FolderHoverPreview;

internal sealed class PreviewWindow : Window
{
    private readonly Border _frame;
    private readonly StackPanel _content;
    private readonly TextBlock _heading;
    private readonly WrapPanel _tiles;
    private readonly TextBlock _emptyMessage;
    private PreviewThemePreferences _theme = PreviewThemeConfig.Default;

    public PreviewWindow()
    {
        Title = "Folder preview";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;
        Focusable = true;

        _heading = new TextBlock
        {
            Foreground = CreateBrush(_theme.HeadingText),
            FontFamily = new FontFamily(_theme.FontFamily),
            FontSize = _theme.HeadingFontSize,
            Margin = new Thickness(0, 0, 0, 9),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _tiles = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
        _emptyMessage = new TextBlock
        {
            Foreground = CreateBrush(_theme.EmptyText),
            FontFamily = new FontFamily(_theme.FontFamily),
            FontSize = _theme.EmptyFontSize,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 260,
            Margin = new Thickness(0, 5, 0, 0),
            Text = "Папка пуста или не содержит файлов выбранных форматов."
        };

        _content = new StackPanel();
        _content.Children.Add(_heading);
        _content.Children.Add(_tiles);
        _content.Children.Add(_emptyMessage);

        _frame = new Border
        {
            Background = CreateBrush(_theme.WindowBackground),
            BorderBrush = CreateBrush(_theme.WindowBorder),
            BorderThickness = new Thickness(_theme.BorderThickness),
            CornerRadius = new CornerRadius(_theme.WindowCornerRadius),
            Padding = new Thickness(_theme.WindowPadding),
            Child = _content
        };
        Content = _frame;
        ApplyTheme(_theme);

        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
                Hide();
        };
    }

    public bool ContainsScreenPoint(NativeMethods.Point point)
    {
        var handle = new WindowInteropHelper(this).Handle;
        return handle != IntPtr.Zero
            && NativeMethods.GetWindowRect(handle, out var rect)
            && point.X >= rect.Left && point.X <= rect.Right
            && point.Y >= rect.Top && point.Y <= rect.Bottom;
    }

    public void ApplyTheme(PreviewThemePreferences theme)
    {
        _theme = PreviewThemeConfig.Normalize(theme);
        _frame.Background = CreateBrush(_theme.WindowBackground);
        _frame.BorderBrush = CreateBrush(_theme.WindowBorder);
        _frame.BorderThickness = new Thickness(_theme.BorderThickness);
        _frame.CornerRadius = new CornerRadius(_theme.WindowCornerRadius);
        _frame.Padding = new Thickness(_theme.WindowPadding);
        _frame.Effect = _theme.ShadowEnabled
            ? new DropShadowEffect
            {
                Color = PreviewThemeConfig.ParseColor(_theme.ShadowColor),
                BlurRadius = _theme.ShadowBlur,
                ShadowDepth = 1,
                Opacity = _theme.ShadowOpacity
            }
            : null;
        _heading.Foreground = CreateBrush(_theme.HeadingText);
        _heading.FontSize = _theme.HeadingFontSize;
        _heading.FontFamily = new FontFamily(_theme.FontFamily);
        _emptyMessage.Foreground = CreateBrush(_theme.EmptyText);
        _emptyMessage.FontSize = _theme.EmptyFontSize;
        _emptyMessage.FontFamily = new FontFamily(_theme.FontFamily);
        _emptyMessage.MaxWidth = _theme.TileWidth * Math.Min(_theme.MaxColumns, 2);
        Opacity = _theme.WindowOpacity;
    }

    public void ShowFolder(
        string folderName,
        string folderPath,
        NativeMethods.Point cursor,
        DesktopHit folderBounds,
        IReadOnlySet<string> enabledExtensions)
    {
        _heading.Text = folderName;
        _tiles.Children.Clear();

        var files = GetPreviewFiles(folderPath, enabledExtensions);
        _emptyMessage.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _tiles.Visibility = files.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (files.Count > 0)
        {
            var columns = Math.Clamp(files.Count, 1, _theme.MaxColumns);
            _tiles.Width = columns * (_theme.TileWidth + _theme.TileSpacing);
            foreach (var file in files)
                _tiles.Children.Add(CreateTile(file, _theme));
        }
        else
        {
            _tiles.Width = Math.Min(_theme.TileWidth * Math.Min(_theme.MaxColumns, 2), 480);
        }

        _content.Measure(new Size(900, 1600));
        Width = _content.DesiredSize.Width
            + _frame.Padding.Left + _frame.Padding.Right
            + _frame.BorderThickness.Left + _frame.BorderThickness.Right;
        Height = _content.DesiredSize.Height
            + _frame.Padding.Top + _frame.Padding.Bottom
            + _frame.BorderThickness.Top + _frame.BorderThickness.Bottom;

        Opacity = 0;
        if (!IsVisible)
            Show();
        UpdateLayout();
        PositionNearFolder(cursor, folderBounds);
        Opacity = _theme.WindowOpacity;
    }

    private void PositionNearFolder(NativeMethods.Point cursor, DesktopHit folderBounds)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        // Put the invisible window on the pointer's monitor first so WPF
        // receives the monitor's DPI before calculating its final bounds.
        NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            cursor.X + 16,
            cursor.Y + 12,
            0,
            0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        UpdateLayout();

        var monitor = NativeMethods.MonitorFromPoint(cursor, 2);
        var monitorInfo = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            var fallbackX = (int)Math.Ceiling(folderBounds.Right) + 12;
            var fallbackY = (int)Math.Floor(folderBounds.Top);
            NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                fallbackX,
                fallbackY,
                0,
                0,
                NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
            return;
        }

        var dpi = NativeMethods.GetDpiForWindow(handle);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var width = (int)Math.Ceiling(ActualWidth * scale);
        var height = (int)Math.Ceiling(ActualHeight * scale);
        var work = monitorInfo.Work;
        var folderLeft = (int)Math.Floor(folderBounds.Left);
        var folderTop = (int)Math.Floor(folderBounds.Top);
        var folderRight = (int)Math.Ceiling(folderBounds.Right);
        var folderBottom = (int)Math.Ceiling(folderBounds.Bottom);
        const int margin = 8;
        const int gap = 12;

        var candidates = new[]
        {
            (X: folderRight + gap, Y: folderTop),
            (X: folderLeft - width - gap, Y: folderTop),
            (X: folderLeft, Y: folderBottom + gap),
            (X: folderLeft, Y: folderTop - height - gap)
        };

        var x = 0;
        var y = 0;
        var bestOverlap = long.MaxValue;
        var bestCursorDistance = long.MaxValue;
        foreach (var candidate in candidates)
        {
            var candidateX = ClampToWorkArea(candidate.X, work.Left, work.Right, width, margin);
            var candidateY = ClampToWorkArea(candidate.Y, work.Top, work.Bottom, height, margin);
            var overlapWidth = Math.Max(
                0,
                Math.Min(candidateX + width, folderRight) - Math.Max(candidateX, folderLeft));
            var overlapHeight = Math.Max(
                0,
                Math.Min(candidateY + height, folderBottom) - Math.Max(candidateY, folderTop));
            var overlap = (long)overlapWidth * overlapHeight;
            var dx = (long)candidateX - cursor.X;
            var dy = (long)candidateY - cursor.Y;
            var cursorDistance = dx * dx + dy * dy;

            if (overlap < bestOverlap || (overlap == bestOverlap && cursorDistance < bestCursorDistance))
            {
                x = candidateX;
                y = candidateY;
                bestOverlap = overlap;
                bestCursorDistance = cursorDistance;
            }
        }

        NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            x,
            y,
            0,
            0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    private static int ClampToWorkArea(int coordinate, int start, int end, int size, int margin)
    {
        var minimum = start + margin;
        var maximum = end - size - margin;
        return maximum < minimum ? minimum : Math.Clamp(coordinate, minimum, maximum);
    }

    private static List<string> GetPreviewFiles(string folderPath, IReadOnlySet<string> enabledExtensions)
    {
        var files = new List<string>();
        if (enabledExtensions.Count == 0)
            return files;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (enabledExtensions.Contains(Path.GetExtension(file)))
                    files.Add(file);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }

        return files;
    }

    private static ThemedButton CreateTile(string file, PreviewThemePreferences theme)
    {
        var label = Path.GetFileName(file) ?? file;
        if (label.EndsWith(".exe.lnk", StringComparison.OrdinalIgnoreCase))
            label = label[..^8];
        else
            label = Path.GetFileNameWithoutExtension(label);

        return PreviewTileFactory.Create(
            label,
            theme,
            GetFileIcon(file, theme.IconSize),
            () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                }
                catch (Exception) { }
            });
    }

    private static Brush CreateBrush(string color)
    {
        var brush = new SolidColorBrush(PreviewThemeConfig.ParseColor(color));
        brush.Freeze();
        return brush;
    }

    private static ImageSource GetFileIcon(string file, double iconSize)
    {
        var iconPixels = Math.Clamp((int)Math.Round(iconSize), 16, 128);
        var info = new NativeMethods.ShellFileInfo
        {
            DisplayName = string.Empty,
            TypeName = string.Empty
        };

        NativeMethods.SHGetFileInfo(
            file,
            0,
            ref info,
            (uint)Marshal.SizeOf<NativeMethods.ShellFileInfo>(),
            (uint)(NativeMethods.ShgfiIcon | NativeMethods.ShgfiLargeIcon));

        if (info.Icon == IntPtr.Zero)
            return Imaging.CreateBitmapSourceFromHIcon(
                System.Drawing.SystemIcons.Application.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(iconPixels, iconPixels));

        try
        {
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                info.Icon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(iconPixels, iconPixels));
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            NativeMethods.DestroyIcon(info.Icon);
        }
    }
}
