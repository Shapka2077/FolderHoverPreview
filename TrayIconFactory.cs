using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FolderHoverPreview;

internal static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var outline = new Pen(Color.FromArgb(255, 72, 48, 25), 1.5f);
            using var folderFill = new SolidBrush(Color.FromArgb(255, 235, 174, 61));
            using var tabFill = new SolidBrush(Color.FromArgb(255, 255, 203, 91));

            using var tab = new GraphicsPath();
            tab.AddPolygon(new[]
            {
                new Point(3, 7), new Point(12, 7), new Point(15, 10),
                new Point(28, 10), new Point(28, 14), new Point(3, 14)
            });
            graphics.FillPath(tabFill, tab);
            graphics.DrawPath(outline, tab);

            using var folder = new GraphicsPath();
            folder.AddPolygon(new[]
            {
                new Point(3, 11), new Point(29, 11), new Point(27, 26),
                new Point(4, 26), new Point(2, 15)
            });
            graphics.FillPath(folderFill, folder);
            graphics.DrawPath(outline, folder);

            using var eye = new GraphicsPath();
            eye.AddBezier(new Point(8, 19), new Point(12, 13), new Point(21, 13), new Point(25, 19));
            eye.AddBezier(new Point(25, 19), new Point(21, 25), new Point(12, 25), new Point(8, 19));
            using var eyeFill = new SolidBrush(Color.FromArgb(255, 255, 255, 242));
            using var pupil = new SolidBrush(Color.FromArgb(255, 43, 82, 113));
            using var glint = new SolidBrush(Color.White);
            using var eyeOutline = new Pen(Color.FromArgb(255, 64, 76, 82), 1);
            graphics.FillPath(eyeFill, eye);
            graphics.DrawPath(eyeOutline, eye);
            graphics.FillEllipse(pupil, 14, 15, 6, 7);
            graphics.FillEllipse(glint, 15, 16, 2, 2);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var source = Icon.FromHandle(handle);
            return (Icon)source.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
