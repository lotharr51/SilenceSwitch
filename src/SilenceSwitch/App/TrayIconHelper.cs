using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SilenceSwitch;

public enum TrayIconState
{
    Monitoring,
    SilenceDetected,
    DeviceUnavailable
}

public static class TrayIconHelper
{
    private static readonly Dictionary<TrayIconState, Icon> _cache = new();

    public static Icon GetIcon(TrayIconState state)
    {
        if (_cache.TryGetValue(state, out var cached))
            return cached;

        Icon icon;
        try
        {
            icon = CreateIcon(state);
        }
        catch
        {
            icon = SystemIcons.Application;
        }
        _cache[state] = icon;
        return icon;
    }

    private static Icon CreateIcon(TrayIconState state)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        Color bg = state switch
        {
            TrayIconState.Monitoring => Color.FromArgb(60, 160, 60),
            TrayIconState.SilenceDetected => Color.FromArgb(220, 160, 30),
            TrayIconState.DeviceUnavailable => Color.FromArgb(200, 60, 60),
            _ => Color.Gray
        };

        using var bgBrush = new SolidBrush(bg);
        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

        // Draw headphone shape
        using var pen = new Pen(Color.White, 2.5f);
        // Headband arc
        g.DrawArc(pen, 7, 6, 18, 16, 180, 180);
        // Left ear cup
        g.FillRoundedRectangle(Brushes.White, 5, 15, 6, 10, 2);
        // Right ear cup
        g.FillRoundedRectangle(Brushes.White, 21, 15, 6, 10, 2);

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int w, int h, int r)
    {
        using var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
