using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace AL1_S_Terminal.Win32;

public static class TerminalOverlayInterop {
    public const int OverlayWidth = 200;
    public const int OverlayHeight = 200;

    const uint WsChild = 0x4000_0000;
    const uint WsPopup = 0x8000_0000;

    /// <summary>Child sibling Z-order: insert-after = HWND_TOP (0).</summary>
    static readonly HWND HwndTop = new((nint)0);

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
    /// Saves native style, applies WS_CHILD, calls SetParent, and lays out at the host client-area bottom-left (200×200).
    /// </summary>
    public static bool TryBeginEmbedInTerminalClient(nint terminalHwnd, nint overlayHwnd, out nint capturedStyle) {
        capturedStyle = 0;
        var term = new HWND(terminalHwnd);
        var overlay = new HWND(overlayHwnd);
        if (!PInvoke.IsWindow(term) || !PInvoke.IsWindow(overlay))
            return false;

        capturedStyle = NativeWindowLong.GetWindowLongPtr(overlayHwnd, NativeWindowLong.GwlStyle);

        var wsPopup = unchecked((nint)(long)(ulong)WsPopup);
        var style = unchecked((capturedStyle | (nint)(ulong)WsChild) & ~wsPopup);
        _ = NativeWindowLong.SetWindowLongPtr(overlayHwnd, NativeWindowLong.GwlStyle, style);

        _ = PInvoke.SetParent(overlay, term);

        return TryLayoutEmbeddedInTerminalClient(terminalHwnd, overlayHwnd);
    }

    /// <summary>Moves an already child overlay to another terminal top-level window.</summary>
    public static bool TrySwitchEmbedParent(nint overlayHwnd, nint newTerminalHwnd) {
        var overlay = new HWND(overlayHwnd);
        var term = new HWND(newTerminalHwnd);
        if (!PInvoke.IsWindow(overlay) || !PInvoke.IsWindow(term))
            return false;

        _ = PInvoke.SetParent(overlay, term);

        return TryLayoutEmbeddedInTerminalClient(newTerminalHwnd, overlayHwnd);
    }

    /// <summary>
    /// Positions the overlay at bottom-left inside the terminal's client area (coordinates relative to host).
    /// </summary>
    public static bool TryLayoutEmbeddedInTerminalClient(nint terminalHwnd, nint overlayHwnd) {
        var term = new HWND(terminalHwnd);
        var overlay = new HWND(overlayHwnd);
        if (!PInvoke.IsWindow(term) || !PInvoke.IsWindow(overlay))
            return false;

        if (!PInvoke.GetClientRect(term, out var rc))
            return false;

        var clientH = rc.bottom - rc.top;
        var y = clientH - OverlayHeight;

        var flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
                    | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;

        _ = PInvoke.SetWindowPos(overlay, HwndTop, 0, y, OverlayWidth, OverlayHeight, flags);
        return true;
    }

    /// <summary>
    /// Detaches from the host and restores the style captured in <see cref="TryBeginEmbedInTerminalClient"/>.
    /// </summary>
    public static void EndEmbedOverlay(nint overlayHwnd, nint capturedStyle) {
        if (overlayHwnd == 0)
            return;

        var overlay = new HWND(overlayHwnd);
        if (!PInvoke.IsWindow(overlay))
            return;

        _ = PInvoke.SetParent(overlay, HWND.Null);

        _ = NativeWindowLong.SetWindowLongPtr(overlayHwnd, NativeWindowLong.GwlStyle, capturedStyle);
    }
}
