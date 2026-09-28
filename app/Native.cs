using System.Runtime.InteropServices;

namespace Glaze;

internal static class Native
{
    public const int WM_NCCALCSIZE = 0x0083;
    public const int SM_CYFRAME = 33, SM_CXPADDEDBORDER = 92;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_TRANSIENTWINDOW = 3; // acrylic

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    public static void DarkTitleBar(IntPtr hwnd)
    {
        var on = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    }

    /// <summary>Windows 11's acrylic behind the whole window; the content must be transparent to show it.</summary>
    public static void Acrylic(IntPtr hwnd)
    {
        DarkTitleBar(hwnd);
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
        var backdrop = DWMSBT_TRANSIENTWINDOW;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }
}
