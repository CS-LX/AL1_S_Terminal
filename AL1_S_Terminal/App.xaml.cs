using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace AL1_S_Terminal;

public partial class App : System.Windows.Application {
    NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.Hide();

        var menu = new ContextMenuStrip();
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
