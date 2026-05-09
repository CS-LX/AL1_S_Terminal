using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

public partial class MainWindow : Window {
    /// <summary>~60 Hz position follow.</summary>
    readonly DispatcherTimer _attachTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    TerminalOverlayForm? _overlayForm;
    nint _overlayOwnerHostingHwnd;

    public MainWindow() {
        InitializeComponent();

        _attachTimer.Tick += (_, _) => SyncTerminalOverlay();

        SourceInitialized += (_, _) => _attachTimer.Start();
        Closing += (_, _) => DisposeOverlayForm();
        Closed += (_, _) => _attachTimer.Stop();
    }

    void DisposeOverlayForm() {
        if (_overlayForm is null)
            return;

        _overlayForm.Dispose();
        _overlayForm = null;
        _overlayOwnerHostingHwnd = 0;
        OverlayDebugInfo.OverlayWindowHandle = 0;
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
            }
            catch {
                DisposeOverlayForm();
                return;
            }
        }

        var h = (nint)_overlayForm!.Handle;
        OverlayDebugInfo.OverlayWindowHandle = h;

        TerminalOverlayInterop.TryPositionOverlayScreen(anchorHwnd, h);
        TerminalOverlayInterop.TryForceOverlayPixelSize(h);

        if (IsVisible)
            Hide();
    }

    sealed class TerminalHwndOwner : IWin32Window {
        public IntPtr Handle { get; }

        public TerminalHwndOwner(nint hwnd) => Handle = (IntPtr)hwnd;
    }
}
