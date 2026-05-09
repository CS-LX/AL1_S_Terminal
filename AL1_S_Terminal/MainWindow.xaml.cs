using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using AL1_S_Terminal.TerminalInput;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

public partial class MainWindow : Window {
    readonly DispatcherTimer _attachTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    TerminalOverlayForm? _overlayForm;
    nint _overlayOwnerHostingHwnd;
    TerminalKeyboardActivityHook? _keyboardActivityHook;
    TerminalOverlayAnimationAutoCoordinator? _overlayAnimationAutoCoordinator;
    const int TypingActivityTtlMs = 450;

    public MainWindow() {
        InitializeComponent();

        _attachTimer.Tick += (_, _) => SyncTerminalOverlay();

        SourceInitialized += (_, _) => {
            EnsureTerminalInputAutomation();
            _attachTimer.Start();
        };
        Closing += (_, _) => DisposeOverlayForm();
        Closed += (_, _) => {
            _attachTimer.Stop();
            DisposeTerminalInputAutomation();
        };
    }

    void EnsureTerminalInputAutomation() {
        if (_overlayAnimationAutoCoordinator is not null)
            return;

        try {
            _keyboardActivityHook = new TerminalKeyboardActivityHook(() =>
                _overlayOwnerHostingHwnd != 0
                && TerminalForegroundInterop.IsForegroundInTerminalSubtree(_overlayOwnerHostingHwnd));
        }
        catch {
            _keyboardActivityHook = null;
        }

        _overlayAnimationAutoCoordinator = new TerminalOverlayAnimationAutoCoordinator(
            () => _overlayOwnerHostingHwnd != 0
                  && TerminalForegroundInterop.IsForegroundInTerminalSubtree(_overlayOwnerHostingHwnd),
            () => _keyboardActivityHook?.IsRecentlyTyping(TypingActivityTtlMs) == true,
            TrySetOverlayAnimationState);
    }

    void DisposeTerminalInputAutomation() {
        _overlayAnimationAutoCoordinator = null;
        _keyboardActivityHook?.Dispose();
        _keyboardActivityHook = null;
    }

    /// <summary>若 overlay 已创建且未释放，切换动画状态；否则安全返回 false。</summary>
    internal bool TrySetOverlayAnimationState(string state) {
        if (_overlayForm is null || _overlayForm.IsDisposed)
            return false;

        return _overlayForm.Animator.TrySetState(state, restart: true);
    }

    void DisposeOverlayForm() {
        if (_overlayForm is null)
            return;

        _overlayForm.Dispose();
        _overlayForm = null;
        _overlayOwnerHostingHwnd = 0;
        _overlayAnimationAutoCoordinator?.Reset();
#if DEBUG
        OverlayDebugInfo.OverlayWindowHandle = 0;
#endif
    }

    void SyncTerminalOverlay() {
        if (!TerminalOverlayInterop.TryFindWindowsTerminalWindow(out var hostingHwnd)) {
            DisposeOverlayForm();
            Hide();
            return;
        }

        TerminalOverlayInterop.ResolveTerminalAnchorHwnd(hostingHwnd, out var anchorHwnd);

        var needNewOverlay = _overlayForm is null
                             || _overlayForm.IsDisposed
                             || _overlayOwnerHostingHwnd != hostingHwnd;

        if (needNewOverlay) {
            DisposeOverlayForm();

            try {
                _overlayForm = new TerminalOverlayForm();
                // Owned window stays above its owner in Z-order (fixes WinUI painting over a plain top-level overlay).
                _overlayForm.Show(new TerminalHwndOwner((nint)hostingHwnd));
                _overlayOwnerHostingHwnd = hostingHwnd;
                _overlayAnimationAutoCoordinator?.Reset();
            }
            catch {
                DisposeOverlayForm();
                return;
            }
        }

        var h = (nint)_overlayForm!.Handle;
#if DEBUG
        OverlayDebugInfo.OverlayWindowHandle = h;
#endif

        var corner = TerminalOverlayDisplayPreferences.Corner;
        TerminalOverlayInterop.TryPositionOverlayScreen(
            anchorHwnd,
            h,
            _overlayForm!.ClientSize.Width,
            _overlayForm.ClientSize.Height,
            corner);
        _overlayForm.SetContentMirrorHorizontal(TerminalOverlayDisplayPreferences.MirrorOverlayContent);

        if (IsVisible)
            Hide();

        _overlayAnimationAutoCoordinator?.Tick();
    }

    sealed class TerminalHwndOwner : IWin32Window {
        public IntPtr Handle { get; }

        public TerminalHwndOwner(nint hwnd) => Handle = (IntPtr)hwnd;
    }
}
