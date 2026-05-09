using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace AL1_S_Terminal;

public partial class App : System.Windows.Application {
    NotifyIcon? _trayIcon;

    static App() {
        // WinForms overlay must use physical pixels; otherwise ClientSize shrinks on high DPI (e.g. 200→172).
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
    }

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.Hide();

        var menu = new ContextMenuStrip();
#if DEBUG
        var copyOverlayHandleItem = new ToolStripMenuItem("复制 overlay 句柄");
        copyOverlayHandleItem.Click += (_, _) => {
            var h = OverlayDebugInfo.OverlayWindowHandle;
            if (h == 0) {
                System.Windows.Forms.Clipboard.SetText("(无有效 overlay 句柄，请先打开 Windows Terminal)");
                return;
            }

            System.Windows.Forms.Clipboard.SetText($"0x{h:X}");
        };
        menu.Items.Add(copyOverlayHandleItem);
#endif
        menu.Items.Add("退出", image: null, (_, _) => Shutdown());

        _trayIcon = new NotifyIcon {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "AL1_S_Terminal",
            ContextMenuStrip = menu,
        };
    }

    protected override void OnExit(ExitEventArgs e) {
        if (_trayIcon is not null) {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        base.OnExit(e);
    }
}
