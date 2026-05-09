using System.Collections.Generic;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace AL1_S_Terminal.Win32;

public static class TerminalOverlayInterop {
    /// <summary>Default overlay size when config does not specify dimensions.</summary>
    public const int DefaultOverlayWidth = 200;
    public const int DefaultOverlayHeight = 200;

    const string CascadiaHostingWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";
    const string DesktopWindowContentBridgeClass = "Windows.UI.Composition.DesktopWindowContentBridge";

    static readonly List<nint> ChildScratch = new();
    static readonly List<nint> BridgesBuffer = new();

    static nint _bestHwndLoose;
    static int _bestAreaLoose;
    static nint _bestHwndStrict;
    static int _bestAreaStrict;

    static readonly WNDENUMPROC EnumCallback = EnumCallbackImpl;
    static readonly WNDENUMPROC AccumChildrenProc = AccumChildrenImpl;

    /// <summary>
    /// Finds the largest visible top-level window belonging to Windows Terminal (<c>WindowsTerminal.exe</c>),
    /// preferring <see cref="CascadiaHostingWindowClass"/>.
    /// </summary>
    public static bool TryFindWindowsTerminalWindow(out nint hwnd) {
        _bestHwndLoose = 0;
        _bestAreaLoose = 0;
        _bestHwndStrict = 0;
        _bestAreaStrict = 0;

        _ = PInvoke.EnumWindows(EnumCallback, new LPARAM(0));

        hwnd = _bestHwndStrict != 0 ? _bestHwndStrict : _bestHwndLoose;
        return hwnd != 0;
    }

    /// <summary>
    /// Prefer innermost <see cref="DesktopWindowContentBridgeClass"/> under <paramref name="hostingHwnd"/> for
    /// <see cref="GetWindowRect"/> (black client area). Falls back to <paramref name="hostingHwnd"/>.
    /// </summary>
    public static void ResolveTerminalAnchorHwnd(nint hostingHwnd, out nint anchorHwnd) {
        if (TryFindInnermostDesktopWindowContentBridge(hostingHwnd, out var bridge)) {
            anchorHwnd = bridge;
            return;
        }

        anchorHwnd = hostingHwnd;
    }

    /// <summary>
    /// Moves <paramref name="overlayHwnd"/> to the bottom-left of <paramref name="anchorHwnd"/>’s screen rect.
    /// Uses <see cref="SET_WINDOW_POS_FLAGS.SWP_NOZORDER"/> so WinForms <b>owner</b> relationship controls stacking.
    /// </summary>
    public static bool TryPositionOverlayScreen(nint anchorHwnd, nint overlayHwnd, int overlayWidth, int overlayHeight) {
        var anchor = new HWND(anchorHwnd);
        var overlay = new HWND(overlayHwnd);
        if (!PInvoke.IsWindow(anchor) || !PInvoke.IsWindow(overlay))
            return false;

        if (!PInvoke.GetWindowRect(anchor, out var rect))
            return false;

        if (overlayWidth < 1)
            overlayWidth = DefaultOverlayWidth;
        if (overlayHeight < 1)
            overlayHeight = DefaultOverlayHeight;

        var x = rect.left;
        var y = rect.bottom - overlayHeight;

        var flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
                    | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW
                    | SET_WINDOW_POS_FLAGS.SWP_NOZORDER;

        _ = PInvoke.SetWindowPos(overlay, HWND.Null, x, y, overlayWidth, overlayHeight, flags);
        return true;
    }

    static bool TryFindInnermostDesktopWindowContentBridge(nint rootHostingHwnd, out nint bridgeHwnd) {
        bridgeHwnd = 0;
        if (!CollectDesktopWindowContentBridges(rootHostingHwnd))
            return false;

        // prefer innermost bridge(s): not ancestor of any other bridge
        var bestArea = -1;
        for (var i = 0; i < BridgesBuffer.Count; i++) {
            var b = BridgesBuffer[i];
            var inner = true;
            for (var j = 0; j < BridgesBuffer.Count; j++) {
                var c = BridgesBuffer[j];
                if (c == b)
                    continue;
                if (!IsStrictAncestor(b, c))
                    continue;
                inner = false;
                break;
            }

            if (!inner)
                continue;

            var ch = new HWND(b);
            if (!PInvoke.GetWindowRect(ch, out var rect))
                continue;
            var area = (rect.right - rect.left) * (rect.bottom - rect.top);
            if (area > bestArea) {
                bestArea = area;
                bridgeHwnd = b;
            }
        }

        if (bridgeHwnd != 0)
            return true;

        // fallback: largest bridge
        for (var i = 0; i < BridgesBuffer.Count; i++) {
            var h = BridgesBuffer[i];
            var ch = new HWND(h);
            if (!PInvoke.GetWindowRect(ch, out var rect))
                continue;
            var area = (rect.right - rect.left) * (rect.bottom - rect.top);
            if (area > bestArea) {
                bestArea = area;
                bridgeHwnd = h;
            }
        }

        return bridgeHwnd != 0;
    }

    static bool CollectDesktopWindowContentBridges(nint rootHostingHwnd) {
        BridgesBuffer.Clear();
        var queue = new Queue<nint>();
        queue.Enqueue(rootHostingHwnd);

        while (queue.Count > 0) {
            var cur = queue.Dequeue();

            EnumImmediateChildren(cur);
            for (var i = 0; i < ChildScratch.Count; i++) {
                var child = ChildScratch[i];
                queue.Enqueue(child);

                var ch = new HWND(child);
                if (!PInvoke.IsWindowVisible(ch))
                    continue;

                if (!TryClassNameEquals(ch, DesktopWindowContentBridgeClass))
                    continue;

                if (!PInvoke.GetWindowRect(ch, out var rect))
                    continue;

                var w = rect.right - rect.left;
                var h = rect.bottom - rect.top;
                if (w < 80 || h < 80)
                    continue;

                BridgesBuffer.Add(child);
            }
        }

        return BridgesBuffer.Count > 0;
    }

    static unsafe bool IsStrictAncestor(nint ancestor, nint descendant) {
        var walk = PInvoke.GetParent(new HWND(descendant));
        while ((nint)walk.Value != 0) {
            if ((nint)walk.Value == ancestor)
                return true;
            walk = PInvoke.GetParent(walk);
        }

        return false;
    }

    static void EnumImmediateChildren(nint parentHwnd) {
        ChildScratch.Clear();
        _ = PInvoke.EnumChildWindows(new HWND(parentHwnd), AccumChildrenProc, new LPARAM(0));
    }

    static unsafe BOOL AccumChildrenImpl(HWND hwnd, LPARAM lParamUnused) {
        ChildScratch.Add((nint)hwnd.Value);
        return true;
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
        if (area > _bestAreaLoose) {
            _bestAreaLoose = area;
            _bestHwndLoose = (nint)hwnd.Value;
        }

        if (TryClassNameEquals(hwnd, CascadiaHostingWindowClass) && area > _bestAreaStrict) {
            _bestAreaStrict = area;
            _bestHwndStrict = (nint)hwnd.Value;
        }

        return true;
    }

    static unsafe bool TryClassNameEquals(HWND hwnd, string expected) {
        Span<char> buffer = stackalloc char[256];
        fixed (char* p = buffer) {
            var len = PInvoke.GetClassName(hwnd, p, 256);
            if (len <= 0)
                return false;

            if (len != expected.Length)
                return false;

            return buffer[..len].SequenceEqual(expected.AsSpan());
        }
    }
}
