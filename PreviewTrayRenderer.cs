using DrawingColor = System.Drawing.Color;
using Forms = System.Windows.Forms;

namespace FolderHoverPreview;

internal sealed class PreviewTrayRenderer : Forms.ToolStripProfessionalRenderer
{
    private readonly DrawingColor _selectedBackground;
    private readonly DrawingColor _selectedForeground;
    private readonly DrawingColor _selectedBorder;

    public PreviewTrayRenderer(PreviewThemePreferences theme)
        : base(new ThemeColorTable(theme))
    {
        RoundedEdges = false;
        _selectedBackground = ToDrawingColor(theme.TileHoverBackground);
        _selectedForeground = GetContrastColor(_selectedBackground);
        _selectedBorder = ToDrawingColor(theme.AccentText);
    }

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled || e.Item is Forms.ToolStripSeparator)
        {
            base.OnRenderMenuItemBackground(e);
            return;
        }

        var bounds = new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size);
        using var background = new System.Drawing.SolidBrush(_selectedBackground);
        using var border = new System.Drawing.Pen(_selectedBorder);
        e.Graphics.FillRectangle(background, bounds);
        if (bounds.Width > 1 && bounds.Height > 1)
            e.Graphics.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled)
        {
            base.OnRenderItemText(e);
            return;
        }

        Forms.TextRenderer.DrawText(
            e.Graphics,
            e.Text,
            e.TextFont,
            e.TextRectangle,
            _selectedForeground,
            e.TextFormat);
    }

    private sealed class ThemeColorTable : Forms.ProfessionalColorTable
    {
        private readonly DrawingColor _background;
        private readonly DrawingColor _border;
        private readonly DrawingColor _hover;
        private readonly DrawingColor _pressed;

        public ThemeColorTable(PreviewThemePreferences theme)
        {
            _background = ToDrawingColor(theme.WindowBackground);
            _border = ToDrawingColor(theme.WindowBorder);
            _hover = ToDrawingColor(theme.TileHoverBackground);
            _pressed = ToDrawingColor(theme.TilePressedBackground);
        }

        public override DrawingColor ToolStripDropDownBackground => _background;
        public override DrawingColor MenuBorder => _border;
        public override DrawingColor ToolStripBorder => _border;
        public override DrawingColor MenuItemBorder => _border;
        public override DrawingColor MenuItemSelected => _hover;
        public override DrawingColor MenuItemSelectedGradientBegin => _hover;
        public override DrawingColor MenuItemSelectedGradientEnd => _hover;
        public override DrawingColor MenuItemPressedGradientBegin => _pressed;
        public override DrawingColor MenuItemPressedGradientMiddle => _pressed;
        public override DrawingColor MenuItemPressedGradientEnd => _pressed;
        public override DrawingColor CheckBackground => _hover;
        public override DrawingColor CheckSelectedBackground => _hover;
        public override DrawingColor CheckPressedBackground => _pressed;
        public override DrawingColor ImageMarginGradientBegin => _background;
        public override DrawingColor ImageMarginGradientMiddle => _background;
        public override DrawingColor ImageMarginGradientEnd => _background;
        public override DrawingColor SeparatorDark => _border;
        public override DrawingColor SeparatorLight => _background;
        public override DrawingColor ToolStripGradientBegin => _background;
        public override DrawingColor ToolStripGradientMiddle => _background;
        public override DrawingColor ToolStripGradientEnd => _background;
        public override DrawingColor ToolStripContentPanelGradientBegin => _background;
        public override DrawingColor ToolStripContentPanelGradientEnd => _background;
        public override DrawingColor StatusStripGradientBegin => _background;
        public override DrawingColor StatusStripGradientEnd => _background;

    }

    private static DrawingColor ToDrawingColor(string value)
    {
        var color = PreviewThemeConfig.ParseColor(value);
        return DrawingColor.FromArgb(color.R, color.G, color.B);
    }

    private static DrawingColor GetContrastColor(DrawingColor color)
    {
        static double ChannelLuminance(byte channel)
        {
            var normalized = channel / 255.0;
            return normalized <= 0.04045
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        var luminance =
            0.2126 * ChannelLuminance(color.R)
            + 0.7152 * ChannelLuminance(color.G)
            + 0.0722 * ChannelLuminance(color.B);
        var blackContrast = (luminance + 0.05) / 0.05;
        var whiteContrast = 1.05 / (luminance + 0.05);
        return blackContrast >= whiteContrast ? DrawingColor.Black : DrawingColor.White;
    }
}
