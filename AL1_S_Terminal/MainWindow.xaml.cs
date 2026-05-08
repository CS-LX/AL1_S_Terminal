using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

public partial class MainWindow : Window {
    readonly DispatcherTimer _attachTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public MainWindow() {
        InitializeComponent();

        _attachTimer.Tick += (_, _) => SyncTerminalAttachment();

        SourceInitialized += (_, _) => _attachTimer.Start();
        Closed += (_, _) => _attachTimer.Stop();
    }

    void SyncTerminalAttachment() {
        if (!TerminalOverlayInterop.TryFindWindowsTerminalWindow(out var termHwnd)) {
            Hide();
            return;
        }

        var helper = new WindowInteropHelper(this);
        var overlayHwnd = helper.Handle;
        if (overlayHwnd == IntPtr.Zero)
            return;

        TerminalOverlayInterop.TryPlaceOverlayBottomLeft(termHwnd, overlayHwnd);

        if (!IsVisible)
            Show();
    }
}
