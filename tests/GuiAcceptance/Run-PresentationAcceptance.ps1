[CmdletBinding()]
param([Parameter(Mandatory)][string]$ActualExePath,[Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$Output,[string]$Clip='Wait1',[switch]$ExpectLegacy)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
$exe=(Resolve-Path $ActualExePath).Path;$project=(Resolve-Path $ProjectPath).Path;$out=[IO.Path]::GetFullPath($Output)
if((Test-Path $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw 'Choose empty output.'}
[IO.Directory]::CreateDirectory($out)|Out-Null
$before=(Get-FileHash $project).Hash
$receipt=[ordered]@{Passed=$false;LegacyExpected=[bool]$ExpectLegacy;ExecutableSha256=(Get-FileHash $exe).Hash;ProjectUnchanged=$false;Steps=[Collections.Generic.List[object]]::new();Error=$null}
function Write-Receipt {$receipt|ConvertTo-Json -Depth 7|Set-Content (Join-Path $out 'presentation-acceptance.json') -Encoding UTF8}
function Record([string]$Name,$Data){$receipt.Steps.Add(@{Name=$Name;Data=$Data});Write-Receipt}
function Metric([int]$Code,[int]$Index=0){[FighterGuiAcceptanceNative]::SendMessage($target,$Code,[IntPtr]$Index,[IntPtr]::Zero).ToInt64()}
function Play {
 if((Playback-Text $fighter) -ne 'Stop'){[FighterGuiAcceptanceNative]::PostMessage($button,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
 Wait-Until {(Playback-Text $fighter) -eq 'Stop'} 'playing native animation'|Out-Null
}
function Check-Presentation([string]$Phase){
 Start-Sleep -Milliseconds 350
 $start=Metric 0x84F0;$clock=[Diagnostics.Stopwatch]::StartNew();Start-Sleep -Seconds 2
 $fps=((Metric 0x84F0)-$start)/$clock.Elapsed.TotalSeconds;$mode=Metric 0x84F8
 Record 'presentation' @{Phase=$Phase;FramesPerSecond=$fps;Accelerated=$mode;ViewportOwner=[FighterGuiAcceptanceNative]::WindowProcess($target);
  Playing=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]2,[IntPtr]::Zero).ToInt64();
  TimerRunning=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]3,[IntPtr]::Zero).ToInt64();
  Loop=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]4,[IntPtr]::Zero).ToInt64()}
 Assert ($mode -eq [int](-not $expectedLegacy)) 'Unexpected presentation backend.'
 Assert ($fps -ge $(if($expectedLegacy){25}else{55})) 'Presentation did not recover after the editor operation.'
}
$failure=$null;$fighter=$null
$expectedLegacy=[bool]$ExpectLegacy
try{
 $script:app=Launch-App $project;$fighter=Wait-Until {Fighter-Window $app.Id} 'presentation project'
 Select-Clip $fighter $Clip
 Wait-Until {(Control $fighter 'chkLoop').Current.IsEnabled -and (Control $fighter 'btnPlay').Current.IsEnabled} 'native playback controls ready'|Out-Null
 Pause-Playback $fighter
 $loop=(Control $fighter 'chkLoop').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
 if($loop.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On){$loop.Toggle()}
 $target=[IntPtr](Control $fighter 'modelPanel').Current.NativeWindowHandle;$button=[IntPtr](Control $fighter 'btnPlay').Current.NativeWindowHandle
 $editor=[IntPtr]$fighter.Current.NativeWindowHandle
 Set-Frame $fighter 1
 Play;Check-Presentation 'first play';Pause-Playback $fighter
 $maximum=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]5,[IntPtr]::Zero).ToInt32()
 Set-Frame $fighter 1;Start-Sleep -Milliseconds 300
 $first=Metric 0x84F9;$firstRepeat=Metric 0x84F9
 Assert ($first -eq $firstRepeat) 'Paused native screenshot changed without an edit.'
 Capture $fighter 'native-first.png' -Viewport|Out-Null
 Set-Frame $fighter ([Math]::Max(2,[int]($maximum/2)));Start-Sleep -Milliseconds 300
 $middle=Metric 0x84F9
 Capture $fighter 'native-middle.png' -Viewport|Out-Null
 Assert ($middle -ne $first) 'Native screenshots do not show distinct authored poses.'
 Set-Frame $fighter 1;Start-Sleep -Milliseconds 300
 Assert ((Metric 0x84F9) -eq $first) 'Scrubbing back did not restore the exact native screenshot.'
 Record 'native-screenshot-and-scrub' @{First=$first;Middle=$middle;FirstRestored=$true}
 $pattern=$fighter.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
 $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
 $transform=$fighter.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
 Play
 foreach($size in @(@(1066,689),@(1600,1000),@(1336,889))){
  $transform.Resize($size[0],$size[1]);Check-Presentation ('resize '+($size -join 'x'))
 }
 $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized);Start-Sleep -Seconds 1
 $start=Metric 0x84F0;$app.Refresh();$cpu=$app.TotalProcessorTime.TotalMilliseconds;Start-Sleep -Seconds 2;$app.Refresh()
 $hiddenFrames=(Metric 0x84F0)-$start
 Record 'minimized-idle' @{Frames=$hiddenFrames;CpuMilliseconds=$app.TotalProcessorTime.TotalMilliseconds-$cpu}
 if(-not $ExpectLegacy){Assert ($hiddenFrames -le 1) 'Minimized viewport continued rendering.'}
 $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal);Check-Presentation 'restore'
 Pause-Playback $fighter;Start-Sleep -Seconds 1;$app.Refresh();$handles=$app.HandleCount;$threads=$app.Threads.Count
 for($cycle=0;$cycle -lt 10;$cycle++){Play;Start-Sleep -Milliseconds 120;Pause-Playback $fighter}
 Start-Sleep -Seconds 1;$app.Refresh()
 Record 'repeated-play-stop' @{Cycles=10;HandlesBefore=$handles;HandlesAfter=$app.HandleCount;ThreadsBefore=$threads;ThreadsAfter=$app.Threads.Count}
 Assert ($app.HandleCount -le $handles+64 -and $app.Threads.Count -le $threads+8) 'Play/Stop accumulated active render workers or unbounded handles.'
 $paused=Metric 0x84F0;Start-Sleep -Milliseconds 500
 Assert ((Metric 0x84F0) -eq $paused) 'Paused editor kept repainting without changes.'
 Play;Check-Presentation 'restart after ten stops';Pause-Playback $fighter
 if(-not $ExpectLegacy){
  Play
  [FighterGuiAcceptanceNative]::PostMessage($target,0x84FA,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
  Wait-Until {(Metric 0x84F8) -eq 0} 'simulated presentation loss fallback'|Out-Null
  $expectedLegacy=$true;Check-Presentation 'simulated presentation loss'
  Pause-Playback $fighter;Set-Frame $fighter 1;Start-Sleep -Milliseconds 300
  Assert ((Metric 0x84F9) -eq $first) 'Fallback changed the native rendered pose.'
  Record 'fallback-pose-preserved' @{FirstRestored=$true}
 }
 Assert-Layout $fighter
 Close-App $app $fighter;$receipt.Passed=$true
}catch{$failure=$_;$receipt.Error=$_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace;
 try{if($fighter){Pause-Playback $fighter;Close-App $app $fighter}}catch{$receipt['CleanupError']=$_.Exception.Message}}
finally{$receipt.ProjectUnchanged=(Get-FileHash $project).Hash -eq $before;if(-not $receipt.ProjectUnchanged){$receipt.Passed=$false};Write-Receipt}
if($failure){throw $failure}
