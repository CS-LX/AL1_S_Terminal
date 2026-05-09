namespace AL1_S_Terminal.TerminalInput;

/// <summary>
/// Applies <see cref="OverlayAnimationIdleTypingPolicy"/> through a state sink. Does not reference Win32 or WPF.
/// </summary>
public sealed class TerminalOverlayAnimationAutoCoordinator {
    readonly Func<bool> _isTerminalForeground;
    readonly Func<bool> _isRecentlyTyping;
    readonly Func<string, bool> _tryApplyState;
    string? _lastDesiredLogicalState;

    public TerminalOverlayAnimationAutoCoordinator(
        Func<bool> isTerminalForeground,
        Func<bool> isRecentlyTyping,
        Func<string, bool> tryApplyState) {
        _isTerminalForeground = isTerminalForeground ?? throw new ArgumentNullException(nameof(isTerminalForeground));
        _isRecentlyTyping = isRecentlyTyping ?? throw new ArgumentNullException(nameof(isRecentlyTyping));
        _tryApplyState = tryApplyState ?? throw new ArgumentNullException(nameof(tryApplyState));
    }

    /// <summary>Forget last applied logical state (e.g. after overlay is torn down).</summary>
    public void Reset() => _lastDesiredLogicalState = null;

    /// <summary>Re-evaluate policy and push to sink when the desired logical state changes.</summary>
    public void Tick() {
        var want = OverlayAnimationIdleTypingPolicy.ResolveDesiredState(_isTerminalForeground(), _isRecentlyTyping());
        if (string.Equals(want, _lastDesiredLogicalState, StringComparison.Ordinal))
            return;

        _lastDesiredLogicalState = want;
        if (_tryApplyState(want))
            return;

        if (string.Equals(want, OverlayAnimationIdleTypingPolicy.StateTyping, StringComparison.Ordinal))
            _ = _tryApplyState(OverlayAnimationIdleTypingPolicy.StateIdle);
    }
}
