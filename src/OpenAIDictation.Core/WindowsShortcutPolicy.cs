namespace OpenAIDictation.Core;

[Flags]
public enum ShortcutAction { None = 0, Consume = 1, Toggle = 2, Cancel = 4 }

public readonly record struct ShortcutEvent(int VirtualKey, bool IsDown, bool IsInjected,
    bool WindowsDown, bool ControlDown, bool AltDown, bool ShiftDown, bool SessionActive);

/// <summary>Pure decision logic for the low-level hook. Never records or retains typed text.</summary>
public sealed class WindowsShortcutPolicy
{
    private bool _spaceDown, _spaceCaptured, _escapeHeld;

    public ShortcutAction Process(ShortcutEvent input)
    {
        if (input.IsInjected) return ShortcutAction.None;
        if (input.VirtualKey == 0x20) // Space
        {
            if (input.IsDown)
            {
                if (_spaceDown) return _spaceCaptured ? ShortcutAction.Consume : ShortcutAction.None;
                _spaceDown = true;
                if (!input.WindowsDown && input.ControlDown && input.AltDown && !input.ShiftDown)
                {
                    _spaceCaptured = true;
                    return ShortcutAction.Consume | ShortcutAction.Toggle;
                }
            }
            else
            {
                _spaceDown = false;
                bool captured = _spaceCaptured; _spaceCaptured = false;
                return captured ? ShortcutAction.Consume : ShortcutAction.None;
            }
        }
        if (input.VirtualKey == 0x1B) // Escape: keep its matching release out of the editor after cancellation.
        {
            if (_escapeHeld)
            {
                if (!input.IsDown) _escapeHeld = false;
                return ShortcutAction.Consume;
            }
            if (input.IsDown && input.SessionActive)
            {
                _escapeHeld = true;
                return ShortcutAction.Consume | ShortcutAction.Cancel;
            }
        }
        return ShortcutAction.None;
    }
}
