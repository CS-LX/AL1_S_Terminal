using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace AL1_S_Terminal.Win32;

public static class TerminalOverlayInterop {
    public const int OverlayWidth = 200;
    public const int OverlayHeight = 200;

    static nint _bestHwnd;
    static int _bestArea;

    static readonly WNDENUMPROC EnumCallback = EnumCallbackImpl;

    /// <summary>
    /// Finds the largest visible top-level window belonging to Windows Terminal (<c>WindowsTerminal.exe</c>).
    /// </summary>
    public static bool TryFindWindowsTerminalWindow(out nint hwnd) {
        _bestHwnd = 0;
        _bestArea = 0;

        _ = PInvoke.EnumWindows(EnumCallback, new LPARAM(0));

        hwnd = _bestHwnd;
        return hwnd != 0;
    }

    static unsafe BOOL EnumCallbackImpl(HWND hwnd, LPARAM lParamUnused) {
        if (!PInvoke.IsWindowVisible(hwnd))
            return true;

        _ = PInvoke.GetWindowThreadProcessId(hwnd, out uint pid);
        try {
            using var p = Process.GetProcessById((int)pid);
            if (!string.Equals(p.ProcessName, "WindowsTerminal", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch {
            return true;
        }

        if (!PInvoke.GetWindowRect(hwnd, out var rect))
            return true;

        var w = rect.right - rect.left;
        var h = rect.bottom - rect.top;
        if (w < 80 || h < 80)
            return true;

        var area = w * h;
        if (area > _bestArea) {
            _bestArea = area;
            _bestHwnd = (nint)hwnd.Value;
        }

        return true;
    }

    /// <summary>
    /// Places <paramref name="overlayHwnd"/> at the bottom-left inside the terminal window's screen rectangle (200×200 px).
    /// </summary>
    public static bool TryPlaceOverlayBottomLeft(nint terminalHwnd, nint overlayHwnd) {
        var term = new HWND(terminalHwnd);
        var overlay = new HWND(overlayHwnd);
        if (!PInvoke.IsWindow(term) || !PInvoke.IsWindow(overlay))
            return false;

        if (!PInvoke.GetWindowRect(term, out var rect))
            return false;

        var x = rect.left;
        var y = rect.bottom - OverlayHeight;

        var flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
                    | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;

        _ = PInvoke.SetWindowPos(overlay, term, x, y, OverlayWidth, OverlayHeight, flags);
        return true;
    }
}
