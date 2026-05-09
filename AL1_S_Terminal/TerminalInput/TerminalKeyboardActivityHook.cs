using System.Runtime.InteropServices;

namespace AL1_S_Terminal.TerminalInput;

/// <summary>
/// Records <see cref="Environment.TickCount64"/> on qualifying key presses when <paramref name="shouldRecord"/> is true.
/// Must be created and disposed on the thread that runs a Win32 message pump (WPF UI thread).
/// </summary>
public sealed class TerminalKeyboardActivityHook : IDisposable {
    static TerminalKeyboardActivityHook? _active;

    readonly Func<bool> _shouldRecord;
    readonly User32KeyboardHook.LowLevelKeyboardProc _proc;
    nint _hook;
    long _lastKeyEnvironmentTickMs;

    public TerminalKeyboardActivityHook(Func<bool> shouldRecord) {
        _shouldRecord = shouldRecord ?? throw new ArgumentNullException(nameof(shouldRecord));
        _proc = StaticLowLevelKeyboardProc;
        _active = this;
        // WH_KEYBOARD_LL: hMod must be NULL (0) per Win32 docs.
        _hook = User32KeyboardHook.SetWindowsHookExW(User32KeyboardHook.WhKeyboardLl, _proc, 0, 0);
        if (_hook == 0)
            throw new InvalidOperationException($"SetWindowsHookEx(WH_KEYBOARD_LL) failed: 0x{Marshal.GetLastPInvokeError():X}");
    }

    public long LastKeyEnvironmentTickMs => Interlocked.Read(ref _lastKeyEnvironmentTickMs);

    public bool IsRecentlyTyping(long windowMs) {
        var last = LastKeyEnvironmentTickMs;
        if (last == 0)
            return false;
        return Environment.TickCount64 - last <= windowMs;
    }

    public void Dispose() {
        if (_hook != 0) {
            _ = User32KeyboardHook.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        if (ReferenceEquals(_active, this))
            _active = null;
    }

    static nint StaticLowLevelKeyboardProc(int nCode, nint wParam, nint lParam) {
        if (nCode >= 0 && _active is { } inst) {
            var msg = (uint)wParam;
            if (msg is User32KeyboardHook.WmKeydown or User32KeyboardHook.WmSyskeydown && inst._shouldRecord())
                Interlocked.Exchange(ref inst._lastKeyEnvironmentTickMs, Environment.TickCount64);
        }

        return User32KeyboardHook.CallNextHookEx(0, nCode, wParam, lParam);
    }
}
