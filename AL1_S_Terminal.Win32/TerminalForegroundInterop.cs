using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace AL1_S_Terminal.Win32;

/// <summary>
/// Foreground / focus helpers for Windows Terminal hosting HWND (no WPF / overlay references).
/// </summary>
public static class TerminalForegroundInterop {
    /// <summary>
    /// True when the foreground window is the Cascadia hosting root or any of its descendants
    /// (covers WinUI content under the same top-level host).
    /// </summary>
    public static unsafe bool IsForegroundInTerminalSubtree(nint cascadiaHostingHwnd) {
        if (cascadiaHostingHwnd == 0)
            return false;

        var host = new HWND(cascadiaHostingHwnd);
        if (!PInvoke.IsWindow(host))
            return false;

        var fg = PInvoke.GetForegroundWindow();
        if (fg.Value is null)
            return false;

        if ((nint)fg.Value == cascadiaHostingHwnd)
            return true;

        return PInvoke.IsChild(host, fg);
    }
}
