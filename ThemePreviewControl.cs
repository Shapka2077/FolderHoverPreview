using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingText = System.Drawing.Text;
using Forms = System.Windows.Forms;

namespace FolderHoverPreview;

internal sealed class ThemePreviewControl : Forms.Control
{
    private PreviewThemePreferences _theme = PreviewThemeConfig.Default;

    public ThemePreviewControl()
    {
        SetStyle(
            Forms.ControlStyles.AllPaintingInWmPaint
            | Forms.ControlStyles.OptimizedDoubleBuffer
            | Forms.ControlStyles.UserPaint
            | Forms.ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Drawing.Color.Transparent;
    }

    public PreviewThemePreferences Theme
    {
        get => _theme;
        set
        {
            _theme = PreviewThemeConfig.Normalize(value);
            Invalidate();
        }
    }

    protected override void OnPaint(Forms.PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = DrawingText.TextRenderingHint.ClearTypeGridFit;

        var scale = Math.Min(Width / 770f, Height / 76f);
        var margin = Math.Max(3f, 4f * scale);
        var windowBounds = new Drawing.RectangleF(
            margin,
            margin,
            Math.Max(1, Width - margin * 2),
            Math.Max(1, Height - margin * 2));
        var windowRadius = (float)_theme.WindowCornerRadius * scale;
        var opacity = _theme.WindowOpacity;

        if (_theme.ShadowEnabled && _theme.ShadowOpacity > 0)
        {
            var shadowColor = WithOpacity(ToDrawingColor(_theme.ShadowColor), opacity * _theme.ShadowOpacity * 0.55);
            var blur = (float)_theme.ShadowBlur * scale;
            for (var layer = 3; layer >= 1; layer--)
            {
                var spread = blur * layer / 3f;
                var shadowBounds = windowBounds;
                shadowBounds.Inflate(spread * 0.4f, spread * 0.25f);
                shadowBounds.Offset(0, Math.Max(1f, spread * 0.3f));
                using var shadowBrush = new Drawing.SolidBrush(
                    Drawing.Color.FromArgb(
                        Math.Max(1, shadowColor.A / (4 - layer)),
                        shadowColor.R,
                        shadowColor.G,
                        shadowColor.B));
                FillRoundedRectangle(graphics, shadowBrush, shadowBounds, windowRadius + spread);
            }
        }

        using (var windowBrush = new Drawing.SolidBrush(
                   WithOpacity(ToDrawingColor(_theme.WindowBackground), opacity)))
        {
            FillRoundedRectangle(graphics, windowBrush, windowBounds, windowRadius);
        }

        if (_theme.BorderThickness > 0)
        {
            using var borderPen = new Drawing.Pen(
                WithOpacity(ToDrawingColor(_theme.WindowBorder), opacity),
                Math.Max(0.5f, (float)_theme.BorderThickness * scale));
            DrawRoundedRectangle(
                graphics,
                borderPen,
                windowBounds,
                Math.Max(0, windowRadius - borderPen.Width / 2));
        }

        var padding = Math.Clamp((float)_theme.WindowPadding * scale, 5f, 16f);
        var headingFontSize = Math.Clamp((float)_theme.HeadingFontSize * 0.68f, 6f, 12f);
        using var headingFont = CreateFont(headingFontSize);
        using var headingBrush = new Drawing.SolidBrush(
            WithOpacity(ToDrawingColor(_theme.HeadingText), opacity));
        var headingBounds = new Drawing.RectangleF(
            windowBounds.Left + padding,
            windowBounds.Top + 3f * scale,
            windowBounds.Width - padding * 2,
            headingFont.GetHeight(graphics) + 2f * scale);
        graphics.DrawString(
            "Рабочая папка",
            headingFont,
            headingBrush,
            headingBounds,
            Drawing.StringFormat.GenericDefault);

        var tileCount = Math.Clamp(_theme.MaxColumns, 1, 3);
        var tileGap = Math.Max(1f, (float)_theme.TileSpacing * scale * 0.7f);
        var availableWidth = windowBounds.Width - padding * 2;
        var tileWidth = Math.Min(
            (float)_theme.TileWidth * scale * 0.76f,
            (availableWidth - tileGap * (tileCount - 1)) / tileCount);
        var tileTop = headingBounds.Bottom + 2f * scale;
        var tileHeight = Math.Max(8f, windowBounds.Bottom - tileTop - 4f * scale);
        var tileRadius = (float)_theme.TileCornerRadius * scale;
        var tileNames = new[] { "Документ", "Изображение", "Архив" };
        var tileMarks = new[] { "TXT", "IMG", "ZIP" };

        for (var index = 0; index < tileCount; index++)
        {
            var left = windowBounds.Left + padding + index * (tileWidth + tileGap);
            var tileBounds = new Drawing.RectangleF(left, tileTop, tileWidth, tileHeight);
            var background = index switch
            {
                1 => _theme.TileHoverBackground,
                2 => _theme.TilePressedBackground,
                _ => _theme.TileBackground
            };
            using (var tileBrush = new Drawing.SolidBrush(
                       WithOpacity(ToDrawingColor(background), opacity)))
            {
                FillRoundedRectangle(graphics, tileBrush, tileBounds, tileRadius);
            }

            var textColor = index == 1 ? _theme.AccentText : _theme.TileText;
            using var textBrush = new Drawing.SolidBrush(
                WithOpacity(ToDrawingColor(textColor), opacity));
            using var iconPen = new Drawing.Pen(
                WithOpacity(ToDrawingColor(_theme.AccentText), opacity),
                Math.Max(0.6f, scale));
            var iconSize = Math.Clamp(
                Math.Min((float)_theme.IconSize * scale * 0.42f, tileHeight * 0.34f),
                5f,
                17f);
            var iconBounds = new Drawing.RectangleF(
                tileBounds.Left + (tileBounds.Width - iconSize * 1.2f) / 2,
                tileBounds.Top + 2f * scale,
                iconSize * 1.2f,
                iconSize);
            graphics.DrawRectangle(
                iconPen,
                iconBounds.X,
                iconBounds.Y,
                iconBounds.Width,
                iconBounds.Height);

            using var iconFont = CreateFont(Math.Clamp(5f * scale, 4f, 7f));
            var markBounds = new Drawing.RectangleF(
                iconBounds.Left,
                iconBounds.Top + (iconBounds.Height - iconFont.GetHeight(graphics)) / 2,
                iconBounds.Width,
                iconFont.GetHeight(graphics));
            graphics.DrawString(
                tileMarks[index],
                iconFont,
                textBrush,
                markBounds,
                CenteredText);

            using var captionFont = CreateFont(
                Math.Clamp((float)_theme.TileFontSize * 0.66f, 5f, 9f));
            var captionBounds = new Drawing.RectangleF(
                tileBounds.Left + 2f * scale,
                iconBounds.Bottom + 1f * scale,
                tileBounds.Width - 4f * scale,
                Math.Max(1f, tileBounds.Bottom - iconBounds.Bottom - 2f * scale));
            graphics.DrawString(
                tileNames[index],
                captionFont,
                textBrush,
                captionBounds,
                CenteredText);
        }
    }

    private Drawing.Font CreateFont(float size) =>
        new(_theme.FontFamily, size, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point);

    private static readonly Drawing.StringFormat CenteredText = new()
    {
        Alignment = Drawing.StringAlignment.Center,
        LineAlignment = Drawing.StringAlignment.Center,
        Trimming = Drawing.StringTrimming.EllipsisCharacter,
        FormatFlags = Drawing.StringFormatFlags.NoWrap
    };

    private static Drawing.Color ToDrawingColor(string color)
    {
        var parsed = PreviewThemeConfig.ParseColor(color);
        return Drawing.Color.FromArgb(parsed.R, parsed.G, parsed.B);
    }

    private static Drawing.Color WithOpacity(Drawing.Color color, double opacity) =>
        Drawing.Color.FromArgb(
            Math.Clamp((int)Math.Round(255 * opacity), 0, 255),
            color.R,
            color.G,
            color.B);

    private static void FillRoundedRectangle(
        Drawing.Graphics graphics,
        Drawing.Brush brush,
        Drawing.RectangleF bounds,
        float radius)
    {
        using var path = RoundedRectanglePath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(
        Drawing.Graphics graphics,
        Drawing.Pen pen,
        Drawing.RectangleF bounds,
        float radius)
    {
        using var path = RoundedRectanglePath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static Drawing2D.GraphicsPath RoundedRectanglePath(
        Drawing.RectangleF bounds,
        float radius)
    {
        var path = new Drawing2D.GraphicsPath();
        var diameter = Math.Min(Math.Max(0, radius * 2), Math.Min(bounds.Width, bounds.Height));
        if (diameter < 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Drawing.RectangleF(bounds.Location, new Drawing.SizeF(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
