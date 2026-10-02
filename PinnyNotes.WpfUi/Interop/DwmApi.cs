using System.Runtime.InteropServices;

namespace PinnyNotes.WpfUi.Interop;

internal partial class DwmApi
{
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;

    public const int DWMWCP_ROUND = 2;
    public const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

    /// <summary>
    /// Requests native rounded corners (Windows 11+). The DWM border is hidden as the note
    /// draws its own. Has no effect on older versions of Windows or layered (transparent) windows.
    /// </summary>
    public static void ApplyRoundedCorners(nint hwnd)
    {
        int cornerPreference = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

        uint borderColour = DWMWA_COLOR_NONE;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColour, sizeof(uint));
    }
}
