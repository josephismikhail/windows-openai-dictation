using OpenAIDictation.Windows;

namespace OpenAIDictation.Windows.Tests;

public sealed class EditorReturnTests
{
    private sealed class Target:IEditorReturnTarget
    {
        public bool Current,Valid=true,AllowsActivation=true;
        public int Activations,Selections,Checks;
        public int FailCheck=int.MaxValue;
        public bool IsCurrent()=>Current;
        public bool CanRestore()=>Valid && ++Checks<FailCheck;
        public bool Activate(){Activations++;if(AllowsActivation)Current=true;return AllowsActivation;}
        public void RestoreSelection()=>Selections++;
    }
    [Fact] public async Task FocusSwitchReturnsToOriginalFieldAndSelection()
    {
        var target=new Target();Assert.True(await EditorReturn.RestoreAsync(target,default));
        Assert.Equal(1,target.Activations);Assert.Equal(1,target.Selections);Assert.Equal(3,target.Checks);
    }
    [Fact] public async Task CurrentFieldDoesNotGetReactivatedOrSelectionReset()
    {
        var target=new Target{Current=true};Assert.True(await EditorReturn.RestoreAsync(target,default));
        Assert.Equal(0,target.Activations);Assert.Equal(0,target.Selections);
    }
    [Fact] public async Task MissingOrChangedEditorIsNotActivated()
    {
        var target=new Target{Valid=false};Assert.False(await EditorReturn.RestoreAsync(target,default));
        Assert.Equal(0,target.Activations);Assert.Equal(0,target.Selections);
    }
    [Fact] public async Task DeniedForegroundActivationDoesNotRestoreSelection()
    {
        var target=new Target{AllowsActivation=false};Assert.False(await EditorReturn.RestoreAsync(target,default));
        Assert.Equal(0,target.Selections);
    }
    [Theory] [InlineData(2)] [InlineData(3)]
    public async Task EditorChangesDuringReturnPreventInsertion(int failCheck)
    {
        var target=new Target{FailCheck=failCheck};Assert.False(await EditorReturn.RestoreAsync(target,default));
        Assert.Equal(failCheck==2?0:1,target.Selections);
    }
    [Fact] public async Task CanceledSessionNeverActivatesAnEditor()
    {
        var target=new Target();using var canceled=new CancellationTokenSource();canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>EditorReturn.RestoreAsync(target,canceled.Token));
        Assert.Equal(0,target.Activations);
    }
    [Fact] public void FingerprintDetectsChangesWithoutRetainingTextAndRejectsTruncation()
    {
        string fingerprint=EditorFingerprint.Create("draft message")!;
        Assert.True(EditorFingerprint.Matches(fingerprint,"draft message"));
        Assert.False(EditorFingerprint.Matches(fingerprint,"draft message changed"));
        Assert.False(EditorFingerprint.Matches(fingerprint,"Draft message"));
        Assert.NotNull(EditorFingerprint.Create(new string('x',EditorFingerprint.Limit)));
        Assert.Null(EditorFingerprint.Create(new string('x',EditorFingerprint.Limit+1)));
        Assert.False(EditorFingerprint.Matches(fingerprint,new string('x',EditorFingerprint.Limit+1)));
    }
    [Fact] public void ReadOnlyOrUnknownEditorsCannotBeRestored()
    {
        Assert.True(EditorFingerprint.IsEditable(false));
        Assert.False(EditorFingerprint.IsEditable(true));
        Assert.False(EditorFingerprint.IsEditable(System.Windows.Automation.AutomationElement.NotSupported));
        Assert.False(EditorFingerprint.IsEditable(System.Windows.Automation.TextPattern.MixedAttributeValue));
    }
}
