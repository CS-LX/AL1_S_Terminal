namespace AL1_S_Terminal.TerminalInput;

/// <summary>
/// Pure mapping from focus + typing signals to overlay state names. No Win32 / overlay references.
/// </summary>
public static class OverlayAnimationIdleTypingPolicy {
    public const string StateIdle = "Idle";
    public const string StateTyping = "Typing";

    /// <summary>
    /// When the terminal is not in the foreground, always <see cref="StateIdle"/>.
    /// When foreground on terminal: <see cref="StateTyping"/> if the user is actively typing, otherwise <see cref="StateIdle"/>.
    /// </summary>
    public static string ResolveDesiredState(bool terminalForeground, bool recentlyTyping) {
        if (!terminalForeground)
            return StateIdle;
        return recentlyTyping ? StateTyping : StateIdle;
    }
}
