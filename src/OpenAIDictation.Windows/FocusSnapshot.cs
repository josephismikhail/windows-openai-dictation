using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Security.Cryptography;
using System.Text;

namespace OpenAIDictation.Windows;

/// <summary>Local-only destination identity and bounded fingerprint for restoring the original editor.</summary>
internal sealed record FocusSnapshot(nint Window,nint Control,uint Process,int[]? RuntimeId):IEditorReturnTarget
{
    private AutomationElement? editor;
    private TextPattern? textPattern;
    private TextPatternRange? selection;
    private string? fingerprint;
    public static FocusSnapshot Capture()=>CaptureCore(true);
    private static FocusSnapshot CaptureCore(bool rememberEditor)
    {
        nint window=NativeMethods.GetForegroundWindow();
        uint thread=NativeMethods.GetWindowThreadProcessId(window,out uint process);
        var info=new NativeMethods.GuiThreadInfo{Size=(uint)Marshal.SizeOf<NativeMethods.GuiThreadInfo>()};
        NativeMethods.GetGUIThreadInfo(thread,ref info);
        int[]? id=null;
        AutomationElement? editor=null;TextPattern? pattern=null;TextPatternRange? selection=null;string? fingerprint=null;
        try
        {
            var element=AutomationElement.FocusedElement;
            if(element!=null && element.Current.ProcessId==process)
            {
                if(element.Current.IsPassword)throw new InvalidOperationException("Dictation is not available in password fields.");
                id=element.GetRuntimeId();
                if(rememberEditor && element.Current.IsKeyboardFocusable && element.Current.ControlType is var type
                    && (type==ControlType.Edit || type==ControlType.Document)
                    && element.TryGetCurrentPattern(TextPattern.Pattern,out var supported))
                {
                    try
                    {
                        pattern=(TextPattern)supported;
                        var ranges=pattern.GetSelection();
                        if(ranges.Length==1 && pattern.SupportedTextSelection==SupportedTextSelection.Single
                            && EditorFingerprint.IsEditable(pattern.DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute)))
                        {
                            fingerprint=EditorFingerprint.Create(pattern.DocumentRange.GetText(EditorFingerprint.Limit+1));
                            if(fingerprint!=null){editor=element;selection=ranges[0].Clone();}
                        }
                    }
                    catch(InvalidOperationException){pattern=null;selection=null;fingerprint=null;editor=null;}
                    catch(ArgumentException){pattern=null;selection=null;fingerprint=null;editor=null;}
                    catch(NotSupportedException){pattern=null;selection=null;fingerprint=null;editor=null;}
                }
            }
        }
        catch(ElementNotAvailableException){}catch(UnauthorizedAccessException){}catch(COMException){}
        return new(window,info.Focus,process,id){editor=editor,textPattern=pattern,selection=selection,fingerprint=fingerprint};
    }
    public bool IsCurrent()
    {
        try
        {
            var now=CaptureCore(false);
            return Window!=0 && Window==now.Window && Process==now.Process && Control==now.Control
                && (RuntimeId==null || now.RuntimeId!=null && RuntimeId.SequenceEqual(now.RuntimeId));
        }
        catch{return false;}
    }
    public async Task<bool> RestoreIfUnchangedAsync(CancellationToken ct)
    {
        try
        {
            return await EditorReturn.RestoreAsync(this,ct);
        }
        catch(OperationCanceledException){throw;}
        catch(ElementNotAvailableException){return false;}
        catch(UnauthorizedAccessException){return false;}
        catch(COMException){return false;}
        catch(InvalidOperationException){return false;}
        catch(NotSupportedException){return false;}
        catch(ArgumentException){return false;}
    }
    bool IEditorReturnTarget.CanRestore()
    {
        if(editor==null || textPattern==null || selection==null || fingerprint==null || RuntimeId==null)return false;
        GetWindowThreadProcessId(Window,out uint process);
        return IsWindow(Window) && process==Process && editor.Current.ProcessId==Process
            && editor.Current.IsEnabled && editor.Current.IsKeyboardFocusable && !editor.Current.IsPassword
            && RuntimeId.SequenceEqual(editor.GetRuntimeId())
            && EditorFingerprint.IsEditable(textPattern.DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute))
            && EditorFingerprint.Matches(fingerprint,textPattern.DocumentRange.GetText(EditorFingerprint.Limit+1));
    }
    bool IEditorReturnTarget.Activate()
    {
        if(!SetForegroundWindow(Window))return false;
        editor!.SetFocus();return true;
    }
    void IEditorReturnTarget.RestoreSelection()=>selection!.Select();
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
}

internal interface IEditorReturnTarget
{
    bool IsCurrent();
    bool CanRestore();
    bool Activate();
    void RestoreSelection();
}
internal static class EditorReturn
{
    internal static async Task<bool> RestoreAsync(IEditorReturnTarget target,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(target.IsCurrent())return true;
        if(!target.CanRestore() || !target.Activate())return false;
        ct.ThrowIfCancellationRequested();
        if(!target.CanRestore())return false;
        target.RestoreSelection();
        await Task.Delay(60,ct);
        return target.IsCurrent() && target.CanRestore();
    }
}

internal static class EditorFingerprint
{
    internal const int Limit=200_000;
    internal static string? Create(string text)=>text.Length>Limit?null:Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(text)));
    internal static bool Matches(string fingerprint,string text)=>Create(text) is { } current && current==fingerprint;
    internal static bool IsEditable(object attribute)=>attribute is bool readOnly && !readOnly;
}
