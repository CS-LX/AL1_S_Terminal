using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using AL1_S_Terminal.OverlayAnimations.Assets;
using AL1_S_Terminal.OverlayAnimations.Model;
using AL1_S_Terminal.OverlayAnimations.Rendering;
using AL1_S_Terminal.OverlayAnimations.Runtime;

namespace AL1_S_Terminal.OverlayAnimations.Editor;

/// <summary>
/// Drives live preview in the editor using the same WinForms <see cref="OverlayAnimationControl"/> as the overlay.
/// </summary>
public sealed class EditorPreviewController : IDisposable {
    readonly WindowsFormsHost _host;
    readonly DispatcherTimer _tick;
    readonly Stopwatch _stopwatch = new();
    OverlayImageAtlas? _atlas;
    OverlayAnimationControl? _control;
    OverlayAnimator? _animator;
    bool _running;
    string? _activeState;

    public EditorPreviewController(WindowsFormsHost host) {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _tick.Tick += OnTick;
    }

    /// <summary>
    /// Stops the preview timer and disposes loaded images so files under the editor workspace folder can be deleted on Windows (GDI+ keeps files open while <see cref="OverlayImageAtlas"/> holds them).
    /// </summary>
    public void ReleaseWorkspaceFileLocks() {
        Pause();
        _host.Child = null;
        _atlas?.Dispose();
        _atlas = null;
        _control = null;
        _animator = null;
    }

    public void LoadConfig(OverlayAnimationConfig cfg, string baseDir, string? playStateName = null) {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(baseDir);

        ReleaseWorkspaceFileLocks();

        var cw = Math.Clamp(cfg.Width, 16, 8192);
        var ch = Math.Clamp(cfg.Height, 16, 8192);

        _atlas = new OverlayImageAtlas(cfg.Images, baseDir);
        _control = new OverlayAnimationControl(_atlas) {
            Dock = DockStyle.None,
            Size = new Size(cw, ch),
            Location = new Point(0, 0)
        };
        _host.Child = _control;
        _animator = new OverlayAnimator(cfg);

        var state = playStateName;
        if (string.IsNullOrEmpty(state) || !cfg.States.ContainsKey(state))
            state = cfg.DefaultState;
        if (!string.IsNullOrEmpty(state) && cfg.States.ContainsKey(state))
            _animator.SetState(state, restart: true);
        else
            _animator.PlayDefault();

        _activeState = _animator.CurrentState;
        _stopwatch.Restart();
        _running = true;
        _tick.Start();
        PushFrame();
    }

    public void Pause() {
        _running = false;
        _tick.Stop();
    }

    public void Play() {
        if (_animator is null || _control is null)
            return;
        _stopwatch.Restart();
        _running = true;
        _tick.Start();
        PushFrame();
    }

    public void Restart() {
        if (_animator is null || _control is null)
            return;
        if (!string.IsNullOrEmpty(_activeState))
            _animator.SetState(_activeState, restart: true);
        else
            _animator.PlayDefault();
        _stopwatch.Restart();
        _running = true;
        _tick.Start();
        PushFrame();
    }

    public void SetPreviewState(string? stateName) {
        _activeState = stateName;
        if (_animator is null || string.IsNullOrEmpty(stateName))
            return;
        if (_animator.TrySetState(stateName, restart: true)) {
            _stopwatch.Restart();
            if (_running)
                PushFrame();
        }
    }

    void OnTick(object? sender, EventArgs e) {
        if (!_running || _animator is null || _control is null)
            return;
        var ms = (int)_stopwatch.ElapsedMilliseconds;
        _control.SetSnapshot(_animator.Sample(ms));
    }

    void PushFrame() {
        if (_animator is null || _control is null)
            return;
        _control.SetSnapshot(_animator.Sample((int)_stopwatch.ElapsedMilliseconds));
    }

    public void Dispose() {
        _tick.Tick -= OnTick;
        ReleaseWorkspaceFileLocks();
    }
}

