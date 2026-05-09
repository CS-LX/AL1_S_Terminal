using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using AL1_S_Terminal.OverlayAnimations.Config;
using AL1_S_Terminal.OverlayAnimations.Runtime;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

/// <summary>
/// Top-level borderless WinForms window; positioned in screen space above Windows Terminal (no WinUI embedding).
/// </summary>
sealed class TerminalOverlayForm : Form {
    const int WsExNoactivate = unchecked((int)0x0800_0000);
    const int WsExLayered = unchecked((int)0x0008_0000);

    bool _useLayeredOverlay;
    OverlayAliceExtractSession? _aliceSession;
    LayeredOverlayAnimationHost? _layeredHost;
    readonly IOverlayAnimator _animator;

    /// <summary>Overlay animation controller; call <see cref="IOverlayAnimator.SetState"/> to switch states.</summary>
    public IOverlayAnimator Animator => _animator;

    /// <summary>水平翻转分层 overlay 内容（右下角 dock 时使用）。非分层 fallback 不受此设置影响。</summary>
    public void SetContentMirrorHorizontal(bool mirror) {
        if (_layeredHost is not null)
            _layeredHost.MirrorContentHorizontally = mirror;
    }

    public TerminalOverlayForm() {
        TopLevel = true;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;

        _useLayeredOverlay = false;

        var alicePath = Path.Combine(AppContext.BaseDirectory, "Assets", "overlay_animations", "Default.alice");

        try {
            _aliceSession = OverlayAlicePackage.LoadExtracted(alicePath);
            var cfg = _aliceSession.Config;
            OverlayAnimationConfigLoader.NormalizeOverlayDimensions(cfg);

            _useLayeredOverlay = true;

            // Must run before any code that creates the handle. Otherwise WinForms paints default gray over ULW.
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            BackColor = Color.Black;

            var w = cfg.Width;
            var h = cfg.Height;
            var s = new Size(w, h);
            ClientSize = s;
            MinimumSize = s;
            MaximumSize = s;

            _layeredHost = LayeredOverlayAnimationHost.Attach(this, cfg, _aliceSession.BaseDirectory);
            _layeredHost.PlayDefault();
            _animator = _layeredHost;
        }
        catch (Exception ex) {
            Debug.WriteLine($"[TerminalOverlayForm] Failed to load default overlay animation package: {alicePath}");
            Debug.WriteLine(ex);
            _useLayeredOverlay = false;
            _animator = new NullOverlayAnimator();

            var fallback = new Size(200, 200);
            ClientSize = fallback;
            MinimumSize = fallback;
            MaximumSize = fallback;

            using var packStream = global::System.Windows.Application.GetResourceStream(
                    new Uri("pack://application:,,,/Assets/window_bg.png"))
                ?.Stream;

            if (packStream is not null) {
                using var ms = new MemoryStream();
                packStream.CopyTo(ms);
                ms.Position = 0;
                BackgroundImage = new Bitmap(ms);
            }

            BackgroundImageLayout = ImageLayout.Stretch;
            DoubleBuffered = true;
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _layeredHost?.Dispose();
            _aliceSession?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams {
        get {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoactivate;
            if (_useLayeredOverlay)
                cp.ExStyle |= WsExLayered;
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) {
        if (_useLayeredOverlay)
            return;
        base.OnPaintBackground(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        if (_useLayeredOverlay)
            return;
        base.OnPaint(e);
    }

    protected override void WndProc(ref Message m) {
        const int WM_ERASEBKGND = 0x0014;
        if (_useLayeredOverlay && m.Msg == WM_ERASEBKGND) {
            m.Result = new IntPtr(1);
            return;
        }
        base.WndProc(ref m);
    }

    sealed class NullOverlayAnimator : IOverlayAnimator {
        public string? CurrentState => null;
        public void PlayDefault() { }
        public void SetState(string stateName, bool restart = true) { }
        public bool TrySetState(string stateName, bool restart = true) => false;
        public void Stop() { }
        public void ReloadConfig() { }
    }
}
