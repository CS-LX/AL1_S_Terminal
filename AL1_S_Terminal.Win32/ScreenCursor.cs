using System.Drawing;
using Windows.Win32;

namespace AL1_S_Terminal.Win32;

public readonly record struct ScreenPoint(int X, int Y);

public static class ScreenCursor {
    public static bool TryGetScreenPosition(out ScreenPoint screenPoint) {
        var ok = PInvoke.GetCursorPos(out Point pt);
        if (!ok) {
            screenPoint = default;
            return false;
        }

        screenPoint = new ScreenPoint(pt.X, pt.Y);
        return true;
    }
}
