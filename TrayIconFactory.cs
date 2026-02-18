using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreateSpeakerIcon()
    {
        try
        {
            using var bitmap = new Bitmap(32, 32);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var bodyBrush = new SolidBrush(Color.FromArgb(34, 102, 180));
            using var coneBrush = new SolidBrush(Color.FromArgb(66, 165, 245));
            using var wavePen = new Pen(Color.FromArgb(0, 176, 255), 2.5f);

            var bodyRect = new Rectangle(3, 10, 7, 12);
            graphics.FillRectangle(bodyBrush, bodyRect);

            var cone = new[]
            {
                new PointF(10f, 12f),
                new PointF(18f, 7f),
                new PointF(18f, 25f),
                new PointF(10f, 20f)
            };
            graphics.FillPolygon(coneBrush, cone);

            var waveRect1 = new RectangleF(15f, 9f, 10f, 14f);
            var waveRect2 = new RectangleF(13f, 5f, 15f, 22f);
            graphics.DrawArc(wavePen, waveRect1, -45f, 90f);
            graphics.DrawArc(wavePen, waveRect2, -45f, 90f);

            var iconHandle = bitmap.GetHicon();
            try
            {
                using var unmanagedIcon = Icon.FromHandle(iconHandle);
                return (Icon)unmanagedIcon.Clone();
            }
            finally
            {
                DestroyIcon(iconHandle);
            }
        }
        catch
        {
            return (Icon)SystemIcons.Application.Clone();
        }
    }
}
