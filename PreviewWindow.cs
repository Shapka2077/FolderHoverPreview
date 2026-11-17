using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderHoverPreview;

internal sealed class PreviewWindow : Window
{
    private static readonly Brush WindowBrush = new SolidColorBrush(Color.FromRgb(0x20, 0x21, 0x24));
    private static readonly Brush NormalBrush = new SolidColorBrush(Color.FromRgb(0x20, 0x21, 0x24));
    private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2E, 0x34));
    private static readonly Brush PressedBrush = new SolidColorBrush(Color.FromRgb(0x31, 0x37, 0x40));
    private static readonly Brush HoverTextBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x66));

    private readonly Border _frame;
    private readonly StackPanel _content;
    private readonly TextBlock _heading;
    private readonly WrapPanel _tiles;
    private readonly TextBlock _emptyMessage;

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
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 15,
            Margin = new Thickness(0, 0, 0, 9),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _tiles = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
        _emptyMessage = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
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
            Background = WindowBrush,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 15, 18, 15),
            Child = _content
        };
        Content = _frame;

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
            var columns = Math.Clamp(files.Count, 1, 4);
            _tiles.Width = columns * 116;
            foreach (var file in files)
                _tiles.Children.Add(CreateTile(file));
        }
        else
        {
            _tiles.Width = 240;
        }

        _content.Measure(new Size(900, 1600));
        Width = _content.DesiredSize.Width + _frame.Padding.Left + _frame.Padding.Right + 2;
        Height = _content.DesiredSize.Height + _frame.Padding.Top + _frame.Padding.Bottom + 2;

        Opacity = 0;
        if (!IsVisible)
            Show();
        UpdateLayout();
        PositionNearFolder(cursor, folderBounds);
        Opacity = 1;
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

    private static Button CreateTile(string file)
    {
        var label = Path.GetFileName(file) ?? file;
        if (label.EndsWith(".exe.lnk", StringComparison.OrdinalIgnoreCase))
            label = label[..^8];
        else
            label = Path.GetFileNameWithoutExtension(label);

        var body = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        body.Children.Add(new Image
        {
            Source = GetFileIcon(file),
            Width = 38,
            Height = 38,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 4)
        });
        var caption = new TextBlock
        {
            Text = label,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 54,
            MaxWidth = 100
        };
        body.Children.Add(caption);

        var button = new Button
        {
            Content = body,
            Width = 108,
            MinHeight = 72,
            Margin = new Thickness(0, 0, 8, 5),
            Padding = new Thickness(4),
            Background = NormalBrush,
            Focusable = false,
            Cursor = Cursors.Hand,
            Style = (Style)System.Windows.Application.Current.FindResource("PreviewTileButton")
        };
        button.MouseEnter += (_, _) =>
        {
            button.Background = HoverBrush;
            caption.Foreground = HoverTextBrush;
        };
        button.MouseLeave += (_, _) =>
        {
            button.Background = NormalBrush;
            caption.Foreground = Brushes.White;
        };
        button.PreviewMouseDown += (_, _) => button.Background = PressedBrush;
        button.PreviewMouseUp += (_, _) => button.Background = HoverBrush;
        button.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            }
            catch (Exception) { }
        };

        return button;
    }

    private static ImageSource GetFileIcon(string file)
    {
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
                BitmapSizeOptions.FromWidthAndHeight(38, 38));

        try
        {
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                info.Icon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(38, 38));
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            NativeMethods.DestroyIcon(info.Icon);
        }
    }
}
