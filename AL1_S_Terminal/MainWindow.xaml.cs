using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

public partial class MainWindow : Window {
    readonly DispatcherTimer _attachTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    bool _embedded;
    nint _embedCapturedStyle;
    nint _attachedTerminalHwnd;

    public MainWindow() {
        InitializeComponent();

        _attachTimer.Tick += (_, _) => SyncTerminalAttachment();

        SourceInitialized += (_, _) => _attachTimer.Start();
        Closing += (_, _) => DetachEmbeddedIfNeeded();
        Closed += (_, _) => _attachTimer.Stop();
    }

    void DetachEmbeddedIfNeeded() {
        if (!_embedded)
            return;

        var overlayHwnd = (nint)new WindowInteropHelper(this).Handle;
        if (overlayHwnd != 0)
            TerminalOverlayInterop.EndEmbedOverlay(overlayHwnd, _embedCapturedStyle);

        _embedded = false;
    }

    void SyncTerminalAttachment() {
        var helper = new WindowInteropHelper(this);
        var overlayHwnd = (nint)helper.Handle;
        if (overlayHwnd == 0)
            return;

        if (!TerminalOverlayInterop.TryFindWindowsTerminalWindow(out var termHwnd)) {
            if (_embedded) {
                TerminalOverlayInterop.EndEmbedOverlay(overlayHwnd, _embedCapturedStyle);
                _embedded = false;
            }

            Hide();
            return;
        }

        if (!_embedded) {
            if (!TerminalOverlayInterop.TryBeginEmbedInTerminalClient(termHwnd, overlayHwnd, out var captured))
                return;

            _embedCapturedStyle = captured;
            _embedded = true;
            _attachedTerminalHwnd = termHwnd;
        }
        else if (_attachedTerminalHwnd != termHwnd) {
            if (!TerminalOverlayInterop.TrySwitchEmbedParent(overlayHwnd, termHwnd))
                return;

            _attachedTerminalHwnd = termHwnd;
        }

        TerminalOverlayInterop.TryLayoutEmbeddedInTerminalClient(termHwnd, overlayHwnd);

        if (!IsVisible)
            Show();
    }
}
