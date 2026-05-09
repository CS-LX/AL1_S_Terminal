using System.Runtime.InteropServices;

namespace AL1_S_Terminal.TerminalInput;

/// <summary>Minimal user32 declarations for WH_KEYBOARD_LL (avoid CsWin32 SafeHandle overload mismatch).</summary>
internal static class User32KeyboardHook {
    internal const int WhKeyboardLl = 13;
    internal const uint WmKeydown = 0x0100;
    internal const uint WmSyskeydown = 0x0104;

    internal delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
}
