# Shared real-desktop acceptance helpers. Callers provide exe/out/receipt, Record and Write-Receipt.
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Windows.Forms,System.Drawing
if (-not ('FighterGuiAcceptanceNative' -as [type])) {
    Add-Type -ReferencedAssemblies 'Accessibility.dll' -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using Accessibility;
public static class FighterGuiAcceptanceNative {
    public static string HashSourceFile(string path) {
        using(var stream=new System.IO.FileStream(path,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.Read,1024*1024,System.IO.FileOptions.SequentialScan))
        using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
    }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr window,int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")] public static extern IntPtr SendEditText(IntPtr window,uint message,IntPtr w,string text);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    private delegate bool WindowCallback(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr l);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    public static IntPtr[] OwnedWindows(int process) {
        var windows=new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h,l)=> { uint id; GetWindowThreadProcessId(h,out id); if(id==process && IsWindowVisible(h))windows.Add(h);return true; },IntPtr.Zero);
        return windows.ToArray();
    }
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h,int index);
    public static bool IsMenuPopup(IntPtr h) { int s=GetWindowLong(h,-16);return (s & unchecked((int)0x80000000))!=0 && (s & 0xC00000)!=0xC00000; }
    public static string ClassName(IntPtr h) { var s=new StringBuilder(256);GetClassName(h,s,256);return s.ToString(); }
    public static string WindowText(IntPtr h) { var s=new StringBuilder(512);GetWindowText(h,s,512);return s.ToString(); }
    public static uint WindowProcess(IntPtr window) { uint process; GetWindowThreadProcessId(window,out process); return process; }
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(IntPtr h,uint id,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out object value);
    public static Rect AccessibleBounds(IntPtr h,string name) {
        object value;Guid iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
        if(AccessibleObjectFromWindow(h,0xFFFFFFFC,ref iid,out value)!=0)return new Rect();
        return FindAccessible(value as IAccessible,name,0);
    }
    public static bool InvokeAccessibleAsync(IntPtr h,string name) {
        object value;Guid iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
        if(AccessibleObjectFromWindow(h,0xFFFFFFFC,ref iid,out value)!=0 || !AccessibleAction(value as IAccessible,name,0,false))return false;
        var thread=new System.Threading.Thread(()=>{try{
            object target;Guid id=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
            if(AccessibleObjectFromWindow(h,0xFFFFFFFC,ref id,out target)==0)AccessibleAction(target as IAccessible,name,0,true);
        }catch{}});
        thread.IsBackground=true;thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.Start();return true;
    }
    private static bool AccessibleAction(IAccessible a,string name,int depth,bool invoke) {
        if(a==null || depth>12)return false;
        for(int i=0;i<=a.accChildCount;i++) {
            object id=i;string text=null;try{text=a.get_accName(id);}catch{}
            if(text==name) {int x,y,w,h;a.accLocation(out x,out y,out w,out h,id);int state=Convert.ToInt32(a.get_accState(id));if(w>0&&h>0 && (state & 0x18001)==0){if(invoke)a.accDoDefaultAction(id);return true;}}
            if(i==0)continue;
            IAccessible child=null;try{child=a.get_accChild(id) as IAccessible;}catch{}
            if(AccessibleAction(child,name,depth+1,invoke))return true;
        }
        return false;
    }
    private static Rect FindAccessible(IAccessible a,string name,int depth) {
        if(a==null || depth>8)return new Rect();
        for(int i=0;i<=a.accChildCount;i++) {
            object id=i;string text=null;try{text=a.get_accName(id);}catch{}
            if(text==name) { int x,y,w,h;a.accLocation(out x,out y,out w,out h,id);int state=Convert.ToInt32(a.get_accState(id));if(w>0 && h>0 && (state & 0x18001)==0)return new Rect{Left=x,Top=y,Right=x+w,Bottom=y+h}; }
            if(i==0)continue;
            IAccessible child=null;try{child=a.get_accChild(id) as IAccessible;}catch{}
            var result=FindAccessible(child,name,depth+1);if(result.Right>result.Left)return result;
        }
        return new Rect();
    }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect rect);
    public static IntPtr OwnedWindowAtPoint(int process,int x,int y) {
        IntPtr result=IntPtr.Zero;
        EnumWindows((h,l)=> { uint id; GetWindowThreadProcessId(h,out id); Rect r;
            if(id==process && IsWindowVisible(h) && GetWindowRect(h,out r) && x>=r.Left && x<r.Right && y>=r.Top && y<r.Bottom) {result=h; return false;}
            return true; },IntPtr.Zero);
        return result;
    }
    public static IntPtr FindDialog(int process, string title) {
        IntPtr result=IntPtr.Zero;
        EnumWindows((h,l)=> { uint id; GetWindowThreadProcessId(h,out id);
            if(id==process && IsWindowVisible(h)) { var text=new StringBuilder(512); var kind=new StringBuilder(256);
                GetWindowText(h,text,512); GetClassName(h,kind,256);
                if(text.ToString()==title && kind.ToString()=="#32770") { result=h; return false; } }
            return true; },IntPtr.Zero);
        return result;
    }
}
'@
}
$uia = [System.Windows.Automation.AutomationElement]
$scope = [System.Windows.Automation.TreeScope]
$types = [System.Windows.Automation.ControlType]
. (Join-Path $PSScriptRoot 'Use-NativeUia.ps1')
$script:controlCache=@{}
function Activate-OwnedWindow([IntPtr]$Handle) {
    if ([FighterGuiAcceptanceNative]::IsIconic($Handle)) { [FighterGuiAcceptanceNative]::ShowWindowAsync($Handle,9) | Out-Null; Start-Sleep -Milliseconds 300 }
    $ownerProcess=[FighterGuiAcceptanceNative]::WindowProcess($Handle)
    Assert ($ownerProcess -ne 0) 'The owned activation window no longer exists.'
    # ToolStrip dropdowns deliberately never become the foreground window.
    # Their owner remains foreground; activating the popup would dismiss its menu.
    if ([FighterGuiAcceptanceNative]::GetForegroundWindow() -eq $Handle) { return }
    if ([FighterGuiAcceptanceNative]::IsMenuPopup($Handle) -and [FighterGuiAcceptanceNative]::WindowProcess([FighterGuiAcceptanceNative]::GetForegroundWindow()) -eq $ownerProcess) { return }
    [FighterGuiAcceptanceNative]::SetForegroundWindow($Handle) | Out-Null
    if ([FighterGuiAcceptanceNative]::GetForegroundWindow() -ne $Handle) {
        [FighterGuiAcceptanceNative]::SetWindowPos($Handle,[IntPtr](-1),0,0,0,0,0x43) | Out-Null
        $r=[FighterGuiAcceptanceNative+Rect]::new()
        Assert ([FighterGuiAcceptanceNative]::GetWindowRect($Handle,[ref]$r)) 'Cannot locate the owned activation window.'
        [FighterGuiAcceptanceNative]::SetCursorPos($r.Left+[Math]::Min(160,($r.Right-$r.Left)/2),$r.Top+12) | Out-Null
        Start-Sleep -Milliseconds 80
        [FighterGuiAcceptanceNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
        Start-Sleep -Milliseconds 30
        [FighterGuiAcceptanceNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
        [FighterGuiAcceptanceNative]::SetWindowPos($Handle,[IntPtr](-2),0,0,0,0,0x43) | Out-Null
    }
    Wait-Until { [FighterGuiAcceptanceNative]::WindowProcess([FighterGuiAcceptanceNative]::GetForegroundWindow()) -eq $ownerProcess } 'owned window activation' | Out-Null
}
function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Wait-Until([scriptblock]$Probe, [string]$Description, [int]$Seconds=30) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        try { $value = & $Probe; if ($value) { return $value } }
        catch [System.Windows.Automation.ElementNotAvailableException] { }
        catch {
            $base=$_.Exception.GetBaseException()
            if ($base -isnot [Runtime.InteropServices.COMException] -or $base.HResult.ToString('X8') -notin @('80040201','80131505','80010108','80010001','8001010A','8000FFFF')) { throw }
        }
        Start-Sleep -Milliseconds 150
    } while ($timer.Elapsed.TotalSeconds -lt $Seconds)
    throw "Timed out waiting for $Description. Inspect the application and local receipt."
}
function App-Windows([int]$AppId) {
    foreach($handle in [FighterGuiAcceptanceNative]::OwnedWindows($AppId)) { $uia::FromHandle($handle) }
}
function Find-Control($Root, [string]$Id='', [string]$Name='', $Type=$null) {
    if (-not $Root) { return $null }
    $conditions = [Collections.Generic.List[System.Windows.Automation.Condition]]::new()
    if ($Id) { $conditions.Add([System.Windows.Automation.PropertyCondition]::new($uia::AutomationIdProperty,$Id)) }
    if ($Name) { $conditions.Add([System.Windows.Automation.PropertyCondition]::new($uia::NameProperty,$Name)) }
    if ($Type) { $conditions.Add([System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$Type)) }
    $condition = if ($conditions.Count -eq 1) { $conditions[0] } else { [System.Windows.Automation.AndCondition]::new($conditions.ToArray()) }
    return $Root.FindFirst($scope::Subtree,$condition)
}
function Control($Root, [string]$Id) {
    $cacheKey=[string]$Root.Current.NativeWindowHandle+'|'+$Id
    if ($script:controlCache.ContainsKey($cacheKey)) {
        $cached=$script:controlCache[$cacheKey]
        try {if($cached.Current.NativeWindowHandle){return $cached}}catch{}
        $script:controlCache.Remove($cacheKey)
    }
    $element = Wait-Until {
        # WinForms can replace providers while restoring a project. A stale
        # provider can report HWND 0 or E_UNEXPECTED despite a live owned form.
        $live=$Root;$handle=$Root.Current.NativeWindowHandle
        if(-not $handle -and $script:app){
            $live=Fighter-Window $script:app.Id
            if(-not $live){$script:app.Refresh();if($script:app.HasExited){throw ('The actual editor exited with code '+$script:app.ExitCode)};return $null}
            $handle=$live.Current.NativeWindowHandle
        }
        if(-not $handle){return $null}
        $live=$uia::FromHandle([IntPtr]$handle)
        Find-Control $live -Id $Id
    } ("owned control: "+$Id)
    $script:controlCache[$cacheKey]=$element
    return $element
}
function Process-Control([int]$AppId, [string]$Name) {
    # Some WinForms dropdowns expose live items through MSAA while their UIA
    # counterparts retain zero bounds. Resolve the actual visible popup first.
    foreach($handle in [FighterGuiAcceptanceNative]::OwnedWindows($AppId)) {
        if(-not [FighterGuiAcceptanceNative]::IsMenuPopup($handle)){continue}
        $r=[FighterGuiAcceptanceNative]::AccessibleBounds($handle,$Name)
        if($r.Right -gt $r.Left) {
            return [pscustomobject]@{Current=[pscustomobject]@{Name=$Name;ProcessId=$AppId;IsEnabled=$true;IsOffscreen=$false;NativeWindowHandle=0;ControlType=$types::MenuItem;BoundingRectangle=[Windows.Rect]::new($r.Left,$r.Top,$r.Right-$r.Left,$r.Bottom-$r.Top)}}
        }
    }
    foreach ($window in (App-Windows $AppId)) {
        if ($window.Current.AutomationId -eq 'ModelForm' -and [FighterGuiAcceptanceNative]::IsIconic([IntPtr]$window.Current.NativeWindowHandle)) { Activate-OwnedWindow ([IntPtr]$window.Current.NativeWindowHandle) }
        $element = Find-Control $window -Name $Name
        if ($element -and -not $element.Current.IsOffscreen -and $element.Current.IsEnabled) { return $element }
    }
}
function Invoke-Control($Element) {
    Assert ($null -ne $Element -and $Element.Current.IsEnabled) 'The requested control is absent or disabled.'
    # Direct COM Invoke can block on shell modal dialogs. Use actual pointer input
    # on the exact visible, enabled element belonging to this acceptance process.
    Assert ($Element.Current.ProcessId -eq $script:app.Id -and -not $Element.Current.IsOffscreen) 'Cannot physically invoke an unowned or hidden element.'
    $bounds=$Element.Current.BoundingRectangle
    Assert ($bounds.Width -gt 0 -and $bounds.Height -gt 0) 'The native invocation target has no visible bounds.'
    $owner=[FighterGuiAcceptanceNative]::OwnedWindowAtPoint($app.Id,[int]($bounds.X+$bounds.Width/2),[int]($bounds.Y+$bounds.Height/2))
    Assert ($owner -ne [IntPtr]::Zero) 'No owned native window contains the target.'
    Activate-OwnedWindow $owner
    if ($Element.Current.ControlType -eq $types::Button -and $Element.Current.NativeWindowHandle -ne 0 -and [FighterGuiAcceptanceNative]::ClassName([IntPtr]$Element.Current.NativeWindowHandle) -match '(?i)button') {
        Assert ([FighterGuiAcceptanceNative]::PostMessage([IntPtr]$Element.Current.NativeWindowHandle,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)) 'The owned native button rejected BM_CLICK.'
        Start-Sleep -Milliseconds 100
        return
    }
    if($Element.Current.Name -and [FighterGuiAcceptanceNative]::InvokeAccessibleAsync($owner,$Element.Current.Name)) {
        Start-Sleep -Milliseconds 150
        return
    }
    # A real HWND button accepts BM_CLICK on its UI thread. Posting it avoids
    # modal Invoke blocking and a pointer click racing the native playback timer.
    # ToolStrip items have no HWND and continue to use their actual screen bounds.
    if ($Element.Current.ControlType -eq $types::Button -and $Element.Current.NativeWindowHandle -ne 0 -and [FighterGuiAcceptanceNative]::ClassName([IntPtr]$Element.Current.NativeWindowHandle) -match '(?i)button') {
        Assert ([FighterGuiAcceptanceNative]::PostMessage([IntPtr]$Element.Current.NativeWindowHandle,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)) 'The owned native button rejected BM_CLICK.'
        Start-Sleep -Milliseconds 100
        return
    }
    [FighterGuiAcceptanceNative]::SetCursorPos([int]($bounds.X+$bounds.Width/2),[int]($bounds.Y+$bounds.Height/2)) | Out-Null
    Start-Sleep -Milliseconds 100
    [FighterGuiAcceptanceNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 30
    [FighterGuiAcceptanceNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
}
function Dialog([int]$AppId, [string]$Title) {
    $handle=[FighterGuiAcceptanceNative]::FindDialog($AppId,$Title)
    if ($handle -ne [IntPtr]::Zero) { return $uia::FromHandle($handle) }
}
function Type-OwnedEdit($Edit,[string]$Text,[switch]$Enter) {
    $handle=[IntPtr]$Edit.Current.NativeWindowHandle
    Assert ($handle -ne [IntPtr]::Zero -and [FighterGuiAcceptanceNative]::WindowProcess($handle) -eq $script:app.Id) 'The text target must be an owned native Edit HWND.'
    Assert ([FighterGuiAcceptanceNative]::ClassName($handle) -match '(?i)edit') 'The text target is not a native Edit.'
    [FighterGuiAcceptanceNative]::SendMessage($handle,0xB1,[IntPtr]::Zero,[IntPtr](-1)) | Out-Null
    foreach($character in $Text.ToCharArray()) {
        [FighterGuiAcceptanceNative]::SendMessage($handle,0x102,[IntPtr]([int]$character),[IntPtr]::Zero) | Out-Null
    }
    if($Enter) {
        # Validate the NumericUpDown edit without routing Enter through the
        # editor's global command preprocessing or a later modal dialog.
        [FighterGuiAcceptanceNative]::SendMessage($handle,0x8,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
    }
}
function Set-DialogPath([int]$AppId, [string]$Title, [string]$Path, [string]$ButtonName) {
    $dialog = Wait-Until { Dialog $AppId $Title } "owned '$Title' dialog"
    Activate-OwnedWindow ([IntPtr]$dialog.Current.NativeWindowHandle)
    $edits = @($dialog.FindAll($scope::Descendants,[System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::Edit)))
    $candidates = @($edits | Where-Object { $_.Current.Name -eq 'File name:' } |
        Sort-Object @{Expression={ if ($_.Current.AutomationId -eq '1001') {0} elseif ($_.Current.AutomationId -eq '1148') {1} else {2} }})
    if (-not $candidates.Count) { $candidates = @($edits | Where-Object { $_.Current.AutomationId -in @('1001','1148') }) }
    $committed = $false
    foreach ($edit in $candidates) {
        $pattern = $null
        if ($edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$pattern) -and -not $pattern.Current.IsReadOnly) {
            # Native character messages raise the shell's edit notifications and
            # cannot leak keyboard shortcuts into the model camera.
            # Shell autocomplete can move the caret between individual WM_CHAR
            # messages. Set the real owned Edit once and verify exact readback.
            $editHandle=[IntPtr]$edit.Current.NativeWindowHandle
            Assert ($editHandle -ne [IntPtr]::Zero -and [FighterGuiAcceptanceNative]::WindowProcess($editHandle) -eq $AppId) 'Filename must be an owned native Edit.'
            [FighterGuiAcceptanceNative]::SendMessage($editHandle,0xB1,[IntPtr]::Zero,[IntPtr](-1))|Out-Null
            [FighterGuiAcceptanceNative]::SendEditText($editHandle,0xC2,[IntPtr]1,$Path)|Out-Null
            try { Wait-Until { $pattern.Current.Value -eq $Path } 'typed filename Edit readback' | Out-Null }
            catch { throw ('Filename readback was ['+$pattern.Current.Value+'] for ['+$Path+']. '+$_.Exception.Message) }
            $committed = $true; break
        }
    }
    Assert $committed "No writable filename Edit in '$Title'; a ComboBox parent must not be used."
    $button = $dialog.FindAll($scope::Descendants,[System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::Button)) |
        Where-Object { $_.Current.Name.Replace('&','').Trim() -eq $ButtonName -and $_.Current.BoundingRectangle.Width -ge 50 } |
        Sort-Object @{Expression={$_.Current.BoundingRectangle.Width};Descending=$true} | Select-Object -First 1
    Invoke-Control $button
    Wait-Until { -not (Dialog $AppId $Title) } "'$Title' dialog to close" | Out-Null
}
function Fighter-Window([int]$AppId) {
    foreach ($window in (App-Windows $AppId)) {
        if(-not $window){continue}
        $current=$window.Current
        if(-not $current){continue}
        # The provider can disappear between property reads while Close()
        # destroys the HWND. Capture its value once before converting to IntPtr.
        $windowHandle=$current.NativeWindowHandle
        if(-not $windowHandle){continue}
        if ($current.AutomationId -eq 'ModelForm' -and [FighterGuiAcceptanceNative]::IsIconic([IntPtr]$windowHandle)) { Activate-OwnedWindow ([IntPtr]$windowHandle) }
        if (-not $window.Current.IsOffscreen -and $window.Current.AutomationId -eq 'ModelForm') { return $window }
        if (-not $window.Current.IsOffscreen -and ([string]$window.Current.Name).EndsWith(' — FighterPort Studio V2')) { return $window }
        $candidate = Find-Control $window -Id 'ModelForm' -Type $types::Window
        if ($candidate -and -not $candidate.Current.IsOffscreen -and ([string]$candidate.Current.Name).EndsWith('FighterPort Studio V2')) { return $candidate }
    }
}
function Click-Toolbar($Window, [int]$X) {
    Activate-OwnedWindow ([IntPtr]$Window.Current.NativeWindowHandle)
    $names=@{259='Open Model...';564='Save';607='Export...';653='Undo';805='Retarget to This Model'}
    $ids=@{259='fighterOpenModel';564='fighterSave';607='fighterExport';653='fighterUndo';805='fighterRetarget'}
    Assert $names.ContainsKey($X) 'Unknown toolbar command.'
    $toolbar=Wait-Until {Find-Control $Window -Id 'fighterToolbar'} 'native fighter toolbar'
    if([FighterGuiAcceptanceNative]::InvokeAccessibleAsync([IntPtr]$toolbar.Current.NativeWindowHandle,$names[$X])) {
        Start-Sleep -Milliseconds 150
        return
    }
    $button=Find-Control $toolbar -Id $ids[$X]
    if(-not $button){$button=Find-Control $toolbar -Name $names[$X]}
    if($button){Invoke-Control $button;return}
    Assert ([FighterGuiAcceptanceNative]::InvokeAccessibleAsync([IntPtr]$toolbar.Current.NativeWindowHandle,$names[$X])) ('Missing native toolbar action '+$names[$X])
}
function Invoke-FighterMenu($Window,[string[]]$Path) {
    for($attempt=0;$attempt -lt 4;$attempt++) {
        try {Invoke-FighterMenuOnce $Window $Path;return}
        catch {if($attempt -eq 3){throw};Start-Sleep -Milliseconds 750}
    }
}
function Invoke-FighterMenuOnce($Window,[string[]]$Path) {
    if(-not $Window -or -not $Window.Current -or -not $Window.Current.NativeWindowHandle) {
        $Window=Wait-Until {
            $current=Fighter-Window $script:app.Id
            if($current){return $current}
            App-Windows $script:app.Id | Where-Object {$_.Current.AutomationId -eq 'MainForm' -and $_.Current.NativeWindowHandle} | Select-Object -First 1
        } 'live owned editor menu window'
    }
    Activate-OwnedWindow ([IntPtr]$Window.Current.NativeWindowHandle)
    foreach($handle in [FighterGuiAcceptanceNative]::OwnedWindows($script:app.Id)) {
        if([FighterGuiAcceptanceNative]::IsMenuPopup($handle)) {
            [FighterGuiAcceptanceNative]::SendMessage($handle,0x1F,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
        }
    }
    Start-Sleep -Milliseconds 200
    $findPopup={param($Name)
        foreach($handle in [FighterGuiAcceptanceNative]::OwnedWindows($script:app.Id)) {
            if(-not [FighterGuiAcceptanceNative]::IsMenuPopup($handle)){continue}
            $bounds=[FighterGuiAcceptanceNative]::AccessibleBounds($handle,$Name)
            if($bounds.Right -gt $bounds.Left){return $handle}
        }
    }
    $clickAccessible={param([IntPtr]$Handle,[string]$Name)
        $bounds=[FighterGuiAcceptanceNative]::AccessibleBounds($Handle,$Name)
        Assert ($bounds.Right -gt $bounds.Left) ('Native menu item has no visible accessible bounds: '+$Name)
        $x=[int](($bounds.Left+$bounds.Right)/2);$y=[int](($bounds.Top+$bounds.Bottom)/2)
        Assert ([FighterGuiAcceptanceNative]::OwnedWindowAtPoint($script:app.Id,$x,$y) -ne [IntPtr]::Zero) ('Native menu item is covered: '+$Name)
        [FighterGuiAcceptanceNative]::SetCursorPos($x,$y) | Out-Null
        [FighterGuiAcceptanceNative]::mouse_event(0x2,0,0,0,[UIntPtr]::Zero)
        [FighterGuiAcceptanceNative]::mouse_event(0x4,0,0,0,[UIntPtr]::Zero)
    }
    if(-not (& $findPopup $Path[0])) {
        $toolbar=Find-Control $Window -Id 'fighterToolbar'
        if(-not $toolbar){$toolbar=Control $Window 'menuStrip1'}
        & $clickAccessible ([IntPtr]$toolbar.Current.NativeWindowHandle) 'Fighter'
    }
    foreach($name in $Path) {
        $popup=Wait-Until {& $findPopup $name} ('visible native menu '+$name) 3
        & $clickAccessible $popup $name
        Start-Sleep -Milliseconds 300
    }
}
function Capture($Window, [string]$Name, [switch]$Viewport) {
    Activate-OwnedWindow ([IntPtr]$Window.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 300
    $element = if ($Viewport) { Control $Window 'modelPanel' } else { $Window }
    $bounds = $element.Current.BoundingRectangle
    $bitmap = [Drawing.Bitmap]::new([int]$bounds.Width,[int]$bounds.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap); $path = Join-Path $out $Name
    try { $graphics.CopyFromScreen([int]$bounds.X,[int]$bounds.Y,0,0,$bitmap.Size); $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
function Assert-Layout($Window) {
    $windowBounds = $Window.Current.BoundingRectangle
    $playBounds = (Control $Window 'btnPlay').Current.BoundingRectangle
    $viewportBounds = (Control $Window 'modelPanel').Current.BoundingRectangle
    $toolbarBounds = (Control $Window 'fighterToolbar').Current.BoundingRectangle
    $statusBounds = (Control $Window 'fighterStatusStrip').Current.BoundingRectangle
    Assert ($playBounds.Bottom -le $statusBounds.Top) 'Native playback controls overlap the new status bar.'
    Assert ($viewportBounds.Top -ge $toolbarBounds.Bottom) 'Native viewer content overlaps the new toolbar.'
    $nativeMenu = Find-Control $Window -Id 'menuStrip1'
    if ($nativeMenu) { Assert ($nativeMenu.Current.BoundingRectangle.Top -ge $toolbarBounds.Bottom) 'The native editor menu is covered by the new toolbar.' }
    Record 'native-layout-visible' @{PlaybackBounds=$playBounds.ToString();ViewportBounds=$viewportBounds.ToString();ToolbarBounds=$toolbarBounds.ToString();StatusBounds=$statusBounds.ToString()}
}
function Clip-Rows($Window) {
    $rows=[System.Windows.Automation.OrCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::DataItem),
        [System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::ListItem))
    $list=Find-Control $Window -Id 'listAnims'
    if($list){$list.FindAll($scope::Descendants,$rows)}
}
function Selected-Clip($Window) {
    $selection = (Control $Window 'listAnims').GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
    foreach ($row in $selection.Current.GetSelection()) { return $row.Current.Name }
    return ''
}
function Select-Clip($Window, [string]$Name) {
    if((Selected-Clip $Window) -eq $Name){return}
    $row = Clip-Rows $Window | Where-Object { $_.Current.Name -eq $Name } | Select-Object -First 1
    Assert ($null -ne $row) "Animation missing: $Name"
    $scroll = $null
    if ($row.TryGetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern,[ref]$scroll)) {
        try{$scroll.ScrollIntoView()}catch{if($_.Exception.GetBaseException().HResult.ToString('X8') -notin @('80040200','80040201')){throw}}
    }
    Wait-Until {
        if ((Selected-Clip $Window) -eq $Name) { return $true }
        # Scroll/rebuild notifications can invalidate a native list-item provider.
        # Reacquire by its current name before each selection attempt.
        $current=Clip-Rows $Window | Where-Object { $_.Current.Name -eq $Name } | Select-Object -First 1
        if ($current) { $current.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
        return (Selected-Clip $Window) -eq $Name
    } "selected animation $Name" | Out-Null
}
function Frame($Window) {
    $slider=Control $Window 'trbFrame'
    return [FighterGuiAcceptanceNative]::SendMessage([IntPtr]$slider.Current.NativeWindowHandle,0x400,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
}
function Playback-Text($Window) {
    return [FighterGuiAcceptanceNative]::WindowText([IntPtr](Control $Window 'btnPlay').Current.NativeWindowHandle)
}
function Pause-Playback($Window) {
    if((Playback-Text $Window) -eq 'Stop'){Invoke-Control (Control $Window 'btnPlay')}
    Wait-Until {(Playback-Text $Window) -eq 'Play'} 'native playback paused' | Out-Null
}
function Set-Frame($Window, [int]$Value) {
    Pause-Playback $Window
    $slider = Control $Window 'trbFrame'
    $maximum=[FighterGuiAcceptanceNative]::SendMessage([IntPtr]$slider.Current.NativeWindowHandle,0x402,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
    Assert ($Value -le $maximum) 'Requested frame exceeds the actual clip length.'
    $spinner=Control $Window 'numFrameIndex'
    $edit=Find-Control $spinner -Type $types::Edit
    Assert ($null -ne $edit) 'The native frame-number Edit is missing.'
    # Real typed frame input raises native NumericUpDown validation and model events.
    # RangeValue.SetValue can move the thumb without updating the rendered pose.
    Type-OwnedEdit $edit ([string]$Value) -Enter
    Wait-Until { (Frame $Window) -eq $Value } "frame $Value" | Out-Null
}
function Save-Project($Window, [string]$Path='') {
    Pause-Playback $Window
    $nativeSave = Find-Control $Window -Id 'btnSaveAnims'
    if ($nativeSave -and $nativeSave.Current.IsEnabled -and -not $nativeSave.Current.IsOffscreen) { Invoke-Control $nativeSave }
    else { Click-Toolbar $Window 564 }
    if ($Path) { Set-DialogPath $script:app.Id 'Save Fighter' $Path 'Save' }
    Wait-Until { $Window.Current.Name -notmatch '\*' } 'saved fighter title' | Out-Null
}
function Launch-App([string]$Project='') {
    $script:controlCache=@{}
    $launch = @{FilePath=$exe;WorkingDirectory=(Split-Path -Parent $exe);WindowStyle='Normal';PassThru=$true;
        RedirectStandardOutput=(Join-Path $out ("launch-$($receipt.Steps.Count).stdout.txt"));RedirectStandardError=(Join-Path $out ("launch-$($receipt.Steps.Count).stderr.txt"))}
    if ($Project) { $launch.ArgumentList = '"' + $Project + '"' }
    $process = Start-Process @launch
    $null=$process.Handle # Retain the process handle so Windows PowerShell can read its exit code after exit.
    Record 'launch-process' @{ProcessId=$process.Id}
    # WinForms can initially expose only Win32 proxies until its first activation.
    # Activate our exact owned caption so the native accessibility provider initializes.
    $window = Wait-Until { App-Windows $process.Id | Where-Object { -not $_.Current.IsOffscreen -and $_.Current.ControlType -eq $types::Window -and $_.Current.AutomationId -in @('MainForm','ModelForm') } | Select-Object -First 1 } 'application startup window'
    [FighterGuiAcceptanceNative]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle) | Out-Null
    [FighterGuiAcceptanceNative]::SetWindowPos([IntPtr]$window.Current.NativeWindowHandle,[IntPtr](-1),0,0,0,0,0x43) | Out-Null
    $bounds=[FighterGuiAcceptanceNative+Rect]::new()
    Assert ([FighterGuiAcceptanceNative]::GetWindowRect([IntPtr]$window.Current.NativeWindowHandle,[ref]$bounds)) 'The startup caption could not be located.'
    [FighterGuiAcceptanceNative]::SetCursorPos($bounds.Left+160,$bounds.Top+12) | Out-Null
    [FighterGuiAcceptanceNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
    [FighterGuiAcceptanceNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
    [FighterGuiAcceptanceNative]::SetWindowPos([IntPtr]$window.Current.NativeWindowHandle,[IntPtr](-2),0,0,0,0,0x43) | Out-Null
    return $process
}
function Close-App($Process, $Fighter) {
    $Fighter.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Wait-Until {
        # Tests save the state they intend to reopen before closing. A later
        # scrub/camera inspection may dirty only that owned test project.
        $question=Dialog $Process.Id 'Close Fighter'
        if($question){
            $discard=Find-Control $question -Name 'No' -Type $types::Button
            Assert ($null -ne $discard) 'The owned close prompt has no No button.'
            Invoke-Control $discard
        }
        -not (Fighter-Window $Process.Id)
    } 'fighter to close' | Out-Null
    Wait-Until {
        foreach ($window in (App-Windows $Process.Id)) {
            if ($window.Current.Name -eq 'FighterPort Studio V2') { $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }
        }
        $Process.Refresh(); $Process.HasExited
    } 'application process to exit' | Out-Null
    $Process.WaitForExit()
    Record 'normal-process-exit' @{ProcessId=$Process.Id;ExitCode=$Process.ExitCode}
    Assert ($Process.ExitCode -eq 0) ('The application exited with code ' + $Process.ExitCode)
}
