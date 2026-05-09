using System.Windows;
using System.Windows.Forms;
using AL1_S_Terminal.OverlayAnimations.Editor;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

#if DEBUG
internal static class OverlayDebugInfo {
    public static nint OverlayWindowHandle { get; set; }
}
#endif

public partial class App : System.Windows.Application {
    NotifyIcon? _trayIcon;
    OverlayAnimationEditorWindow? _editorWindow;

    static App() {
        // WinForms overlay must use physical pixels; otherwise ClientSize shrinks on high DPI (e.g. 200→172).
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
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
        var editorItem = new ToolStripMenuItem("动画编辑器…");
        editorItem.Click += (_, _) => {
            Current.Dispatcher.Invoke(() => {
                if (_editorWindow is not null) {
                    _editorWindow.Activate();
                    return;
                }

                _editorWindow = new OverlayAnimationEditorWindow();
                _editorWindow.Closed += (_, _) => _editorWindow = null;
                _editorWindow.Show();
            });
        };
        menu.Items.Add(editorItem);

        var overlayCornerMenu = new ToolStripMenuItem("Overlay 位置");
        var cornerLeft = new ToolStripMenuItem("左下角", null, (_, _) => SetOverlayCorner(TerminalOverlayScreenCorner.LeftBottom));
        var cornerRight = new ToolStripMenuItem("右下角", null, (_, _) => SetOverlayCorner(TerminalOverlayScreenCorner.RightBottom));
        overlayCornerMenu.DropDownItems.Add(cornerLeft);
        overlayCornerMenu.DropDownItems.Add(cornerRight);
        overlayCornerMenu.DropDownOpening += (_, _) => {
            var c = TerminalOverlayDisplayPreferences.Corner;
            cornerLeft.Checked = c == TerminalOverlayScreenCorner.LeftBottom;
            cornerRight.Checked = c == TerminalOverlayScreenCorner.RightBottom;
        };
        menu.Items.Add(overlayCornerMenu);

        menu.Items.Add("退出", image: null, (_, _) => Shutdown());

        _trayIcon = new NotifyIcon {
            Icon = System.Drawing.SystemIcons.Application,
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

    static void SetOverlayCorner(TerminalOverlayScreenCorner corner) {
        TerminalOverlayDisplayPreferences.Corner = corner;
    }
}
