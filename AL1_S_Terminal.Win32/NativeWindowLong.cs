using System.Runtime.InteropServices;

namespace AL1_S_Terminal.Win32;

internal static class NativeWindowLong {
    internal const int GwlStyle = -16;

    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "SetWindowLongPtrW")]
    internal static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
