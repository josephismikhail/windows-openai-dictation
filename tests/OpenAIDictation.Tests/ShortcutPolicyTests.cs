using OpenAIDictation.Core;

namespace OpenAIDictation.Tests;

public class ShortcutPolicyTests
{
    private static ShortcutEvent Key(int key = 0x20, bool down = true, bool windows = false,
        bool control = true, bool alt = true, bool shift = false, bool active = false, bool injected = false)
        => new(key, down, injected, windows, control, alt, shift, active);

    [Fact] public void ControlAltSpaceTogglesOnceUntilSpaceRelease()
    {
        var policy = new WindowsShortcutPolicy();
        Assert.Equal(ShortcutAction.Consume | ShortcutAction.Toggle, policy.Process(Key()));
        Assert.Equal(ShortcutAction.Consume, policy.Process(Key()));
        Assert.Equal(ShortcutAction.Consume, policy.Process(Key(down:false, control:false, alt:false)));
        Assert.Equal(ShortcutAction.Consume | ShortcutAction.Toggle, policy.Process(Key(active:true)));
    }
    [Theory]
    [InlineData(false,false,false,false)]
    [InlineData(false,true,false,false)]
    [InlineData(false,false,true,false)]
    [InlineData(false,true,true,true)]
    [InlineData(true,true,true,false)]
    [InlineData(true,false,false,false)]
    public void IncompleteOrExtraModifiersKeepOriginalShortcut(bool windows,bool control,bool alt,bool shift)
    {
        var policy = new WindowsShortcutPolicy();
        Assert.Equal(ShortcutAction.None, policy.Process(Key(windows:windows,control:control,alt:alt,shift:shift)));
        Assert.Equal(ShortcutAction.None, policy.Process(Key(down:false)));
    }
    [Theory] [InlineData(0x48)] [InlineData(0x41)] [InlineData(0x5B)] [InlineData(0x5C)]
    public void OrdinaryHWindowsAndUnrelatedKeysPassThrough(int key)
    {
        var policy = new WindowsShortcutPolicy();
        Assert.Equal(ShortcutAction.None, policy.Process(Key(key)));
        Assert.Equal(ShortcutAction.None, policy.Process(Key(key,down:false)));
        Assert.Equal(ShortcutAction.None, policy.Process(Key(0x48,windows:true,control:false,alt:false)));
    }
    [Fact] public void InjectedEventsDoNotAlterHeldKeyState()
    {
        var policy = new WindowsShortcutPolicy();
        Assert.Equal(ShortcutAction.None, policy.Process(Key(injected:true)));
        Assert.Equal(ShortcutAction.Consume | ShortcutAction.Toggle, policy.Process(Key()));
        Assert.Equal(ShortcutAction.None, policy.Process(Key(down:false,injected:true)));
        Assert.Equal(ShortcutAction.Consume, policy.Process(Key()));
        Assert.Equal(ShortcutAction.Consume, policy.Process(Key(down:false)));
    }
    [Fact] public void EscapeCancelsOnlyActiveSessionsAndKeepsRepeatReleasePaired()
    {
        var policy = new WindowsShortcutPolicy();
        Assert.Equal(ShortcutAction.None, policy.Process(Key(0x1B)));
        Assert.Equal(ShortcutAction.Consume | ShortcutAction.Cancel, policy.Process(Key(0x1B,active:true)));
        Assert.Equal(ShortcutAction.Consume, policy.Process(Key(0x1B,active:false)));
        Assert.Equal(ShortcutAction.Consume, policy.Process(Key(0x1B,down:false,active:false)));
        Assert.Equal(ShortcutAction.None, policy.Process(Key(0x1B,down:false,active:false)));
    }
    [Theory] [InlineData(0x11)] [InlineData(0x12)]
    public void ReleasingEitherModifierBeforeSpaceKeepsRepeatAndReleasePaired(int modifier)
    {
        var policy = new WindowsShortcutPolicy();policy.Process(Key());
        Assert.Equal(ShortcutAction.None,policy.Process(Key(modifier,down:false,control:false,alt:false)));
        Assert.Equal(ShortcutAction.Consume,policy.Process(Key(control:false,alt:false)));
        Assert.Equal(ShortcutAction.Consume,policy.Process(Key(down:false,control:false,alt:false)));
        Assert.Equal(ShortcutAction.None,policy.Process(Key(control:false,alt:false)));
        Assert.Equal(ShortcutAction.None,policy.Process(Key(down:false,control:false,alt:false)));
    }
    [Fact] public void AddingModifiersWhileOrdinarySpaceIsAlreadyHeldDoesNotToggle()
    {
        var policy = new WindowsShortcutPolicy();
        Assert.Equal(ShortcutAction.None,policy.Process(Key(control:false,alt:false)));
        Assert.Equal(ShortcutAction.None,policy.Process(Key()));
        Assert.Equal(ShortcutAction.None,policy.Process(Key(down:false)));
        Assert.Equal(ShortcutAction.Consume|ShortcutAction.Toggle,policy.Process(Key()));
    }
}
