[CmdletBinding()]
param([Parameter(Mandatory)][string]$ActualExePath,[Parameter(Mandatory)][string]$ProjectPath,
    [string]$LibraryRoot,[Parameter(Mandatory)][string]$Output,
    [string]$Clip='Wait1',[int]$Seconds=180,[ValidateRange(1,200)][double]$MinimumFps=50,
    [ValidateRange(0,200)][double]$MinimumDisplayedFps=0,[switch]$OpenLibrary,[switch]$KeepVisible,[switch]$Maximized,[switch]$SystemLoad,[string]$PresentMonPath)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PlaybackSoakResources {
 [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process,uint flags);
}
'@
$exe=(Resolve-Path $ActualExePath).Path;$project=(Resolve-Path $ProjectPath).Path;$out=[IO.Path]::GetFullPath($Output)
if($MinimumDisplayedFps -gt 0 -and -not $PresentMonPath){throw 'Displayed FPS acceptance requires an independent presentation trace.'}
if((Test-Path $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw 'Choose empty output.'}
[IO.Directory]::CreateDirectory($out)|Out-Null
if($LibraryRoot){$env:FIGHTERPORT_LIBRARY_ROOT=(Resolve-Path $LibraryRoot).Path};$before=(Get-FileHash $project).Hash
$receipt=[ordered]@{Passed=$false;ExecutableSha256=(Get-FileHash $exe).Hash;Seconds=$Seconds;MinimumFps=$MinimumFps;MinimumDisplayedFps=$MinimumDisplayedFps;OpenLibrary=[bool]$OpenLibrary;ProjectUnchanged=$false;Steps=[Collections.Generic.List[object]]::new();Error=$null}
function Write-Receipt {$receipt|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'playback-soak.json') -Encoding UTF8}
function Record([string]$Name,$Data){$receipt.Steps.Add(@{Name=$Name;Data=$Data});Write-Receipt}
function Read-StopTrace {
 $length=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E8,[IntPtr](-1),[IntPtr]::Zero).ToInt32()
 -join @(for($index=0;$index -lt [Math]::Min($length,12000);$index++){[char][FighterGuiAcceptanceNative]::SendMessage($editor,0x84E8,[IntPtr]$index,[IntPtr]::Zero).ToInt32()})
}
$failure=$null;$fighter=$null;$presentMonitor=$null
try{
 $script:app=Launch-App $project;$fighter=Wait-Until {Fighter-Window $app.Id} 'soak project'
 Select-Clip $fighter $Clip
 Wait-Until {(Control $fighter 'chkLoop').Current.IsEnabled -and (Control $fighter 'btnPlay').Current.IsEnabled} 'native playback controls ready'|Out-Null
 Pause-Playback $fighter
 $loop=(Control $fighter 'chkLoop').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
 if($loop.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On){$loop.Toggle()}
 if($OpenLibrary){$toolbar=[IntPtr](Control $fighter 'fighterToolbar').Current.NativeWindowHandle;Assert ([FighterGuiAcceptanceNative]::InvokeAccessibleAsync($toolbar,'Library')) 'Library action missing';Wait-Until {(Control $fighter 'libraryPage').Current.Name -match '\d+ total'} 'library ready'|Out-Null}
 $button=[IntPtr](Control $fighter 'btnPlay').Current.NativeWindowHandle;$target=[IntPtr](Control $fighter 'modelPanel').Current.NativeWindowHandle
 $editor=[IntPtr]$fighter.Current.NativeWindowHandle
 if($Maximized){[FighterGuiAcceptanceNative]::ShowWindowAsync($editor,3)|Out-Null;Start-Sleep -Milliseconds 500}
 Record 'preview-size' @{Maximized=[bool]$Maximized;Bounds=(Control $fighter 'modelPanel').Current.BoundingRectangle.ToString()}
 $captures=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
 $bakes=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]1,[IntPtr]::Zero).ToInt32()
 if($KeepVisible){[FighterGuiAcceptanceNative]::SetWindowPos($editor,[IntPtr](-1),0,0,0,0,0x43)|Out-Null}
 if($PresentMonPath){
  $captureArgs=@('--process_id',$app.Id,'--timed',([Math]::Ceiling($Seconds+4)),'--terminate_after_timed','--no_console_stats','--session_name',('FighterPort-'+$app.Id),'--output_file',('"'+(Join-Path $out 'presents.csv')+'"'))
  $presentMonitor=Start-Process -FilePath (Resolve-Path $PresentMonPath).Path -ArgumentList $captureArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'presentmon.stdout.txt') -RedirectStandardError (Join-Path $out 'presentmon.stderr.txt')
  $null=$presentMonitor.Handle
 }
 [FighterGuiAcceptanceNative]::PostMessage($button,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Seconds 2
 # Baseline after initial selection, viewport resize and playback warmup settle.
 $cameraBefore=@(for($axis=0;$axis -lt 9;$axis++){[FighterGuiAcceptanceNative]::SendMessage($target,0x84F6,[IntPtr]$axis,[IntPtr]::Zero).ToInt64()})
 $clock=[Diagnostics.Stopwatch]::StartNew()
 while($clock.Elapsed.TotalSeconds -lt $Seconds){
  [FighterGuiAcceptanceNative]::SendMessage($target,0x84F1,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
  [FighterGuiAcceptanceNative]::SendMessage($editor,0x84E5,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
  $poses=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E4,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
  $textUpdates=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E4,[IntPtr]5,[IntPtr]::Zero).ToInt32()
  $frames=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32();$app.Refresh();$cpu=$app.TotalProcessorTime.TotalMilliseconds;$slice=[Diagnostics.Stopwatch]::StartNew()
  $peerBefore=@{};if($SystemLoad){foreach($peer in Get-Process){try{$peerBefore[$peer.Id]=$peer.TotalProcessorTime.TotalMilliseconds}catch{}}}
  Start-Sleep -Milliseconds ([int](1000*[Math]::Min(10,[Math]::Max(0.5,$Seconds-$clock.Elapsed.TotalSeconds))))
  $count=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()-$frames;$duration=$slice.Elapsed.TotalSeconds
  $gap=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F2,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()/1000.0;$app.Refresh()
  $poseCount=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E4,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()-$poses
  Record 'continuous-playback-sample' @{Elapsed=$clock.Elapsed.TotalSeconds;FramesPerSecond=$count/$duration;MaximumSwapGapMs=$gap;CpuMsPerSecond=($app.TotalProcessorTime.TotalMilliseconds-$cpu)/$duration;
   FrameTextUpdatesPerSecond=([FighterGuiAcceptanceNative]::SendMessage($editor,0x84E4,[IntPtr]5,[IntPtr]::Zero).ToInt32()-$textUpdates)/$duration;
   RigCaptures=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()-$captures;
   RetargetBakes=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]1,[IntPtr]::Zero).ToInt32()-$bakes;
   Playing=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]2,[IntPtr]::Zero).ToInt32();TimerRunning=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]3,[IntPtr]::Zero).ToInt32();
   Loop=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]4,[IntPtr]::Zero).ToInt32();MaximumFrame=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]5,[IntPtr]::Zero).ToInt32();CurrentFrame=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]6,[IntPtr]::Zero).ToInt32();
   PosesPerSecond=$poseCount/$duration;MaximumPoseWorkMs=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E4,[IntPtr]1,[IntPtr]::Zero).ToInt32()/1000.0;
   MaximumUiQueueWaitMs=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E4,[IntPtr]2,[IntPtr]::Zero).ToInt32()/1000.0;
   ActivePoseStage=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E4,[IntPtr]3,[IntPtr]::Zero).ToInt32();ActivePoseWorkMs=[FighterGuiAcceptanceNative]::SendMessage($editor,0x84E4,[IntPtr]4,[IntPtr]::Zero).ToInt32()/1000.0;
   MaximumDrawMs=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F7,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()/1000.0;MaximumPresentMs=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F7,[IntPtr]1,[IntPtr]::Zero).ToInt32()/1000.0;
   AcceleratedPresentation=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F8,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32();
   ForegroundProcess=[FighterGuiAcceptanceNative]::WindowProcess([FighterGuiAcceptanceNative]::GetForegroundWindow());EditorMinimized=[FighterGuiAcceptanceNative]::IsIconic($editor);
   PrivateBytes=$app.PrivateMemorySize64;WorkingSet=$app.WorkingSet64;Handles=$app.HandleCount;Threads=$app.Threads.Count;GdiObjects=[PlaybackSoakResources]::GetGuiResources($app.Handle,0);UserObjects=[PlaybackSoakResources]::GetGuiResources($app.Handle,1)}
  if($SystemLoad){
   $peers=@(foreach($peer in Get-Process){try{if($peerBefore.ContainsKey($peer.Id)){[pscustomobject]@{Process=$peer.ProcessName;Id=$peer.Id;CpuMsPerSecond=($peer.TotalProcessorTime.TotalMilliseconds-$peerBefore[$peer.Id])/$duration}}}catch{}})
   $gpu=@(Get-Counter '\GPU Engine(*)\Utilization Percentage' -MaxSamples 1|ForEach-Object {$_.CounterSamples}|Where-Object CookedValue -gt 1|Select-Object InstanceName,CookedValue)
   Record 'system-load' @{Processes=@($peers|Sort-Object CpuMsPerSecond -Descending|Select-Object -First 10);GpuEngines=$gpu}
  }
  if([FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]3,[IntPtr]::Zero).ToInt32() -eq 0){Record 'unexpected-playback-stop' @{Stack=Read-StopTrace};throw 'Playback stopped during the continuous test; see the recorded stop stack.'}
  Assert ($count/$duration -ge $MinimumFps -and $gap -lt 50) "Sustained playback fell below $MinimumFps FPS or stalled for 50ms."
  Assert ([FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32() -eq $captures -and [FighterGuiAcceptanceNative]::SendMessage($editor,0x84E6,[IntPtr]1,[IntPtr]::Zero).ToInt32() -eq $bakes) 'Ordinary native playback invoked rig capture or retarget baking.'
 }
 $cameraAfter=@(for($axis=0;$axis -lt 9;$axis++){[FighterGuiAcceptanceNative]::SendMessage($target,0x84F6,[IntPtr]$axis,[IntPtr]::Zero).ToInt64()})
 Record 'camera-samples' @{Before=$cameraBefore;After=$cameraAfter}
 Assert (($cameraBefore -join ',') -eq ($cameraAfter -join ',')) 'Playback moved the inspection camera.'
 Record 'fixed-camera' @{Unchanged=$true}
 Pause-Playback $fighter;Close-App $app $fighter;$receipt.Passed=$true
}catch{$failure=$_;$receipt.Error=$_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace;
 try{if($fighter){Pause-Playback $fighter;Close-App $app $fighter}}catch{$receipt['CleanupError']=$_.Exception.Message}}
finally{
 if($presentMonitor){if(-not $presentMonitor.WaitForExit(5000)){$presentMonitor.Kill();$receipt['PresentMonTimedOut']=$true}else{$receipt['PresentMonExitCode']=$presentMonitor.ExitCode}}
 if($presentMonitor -and -not $receipt.Contains('PresentMonTimedOut')){
  try{
   Assert ($presentMonitor.ExitCode -eq 0) 'The presentation trace did not complete successfully.'
   $trace=@(Import-Csv -LiteralPath (Join-Path $out 'presents.csv'))
   $culture=[Globalization.CultureInfo]::InvariantCulture
   $last=[double]::Parse($trace[-1].TimeInMs,$culture)
   # Exclude startup and shutdown. Frame-call counters alone missed the reported
   # regression: hundreds of swaps could correspond to very few displayed frames.
   $steady=@($trace|Where-Object { $time=[double]::Parse($_.TimeInMs,$culture);$time -ge 2500 -and $time -lt $last-500 })
   $displayed=@($steady|Where-Object MsUntilDisplayed -ne 'NA')
   Assert ($steady.Count -ge 30) 'The presentation trace has insufficient steady-state samples.'
   $fraction=$displayed.Count/[double]$steady.Count
   $gaps=@($displayed|Where-Object MsBetweenDisplayChange -ne 'NA'|ForEach-Object {[double]::Parse($_.MsBetweenDisplayChange,$culture)})
   $maximum=($gaps|Measure-Object -Maximum).Maximum
   $gaps=@($gaps|Sort-Object);$p99=$gaps[[Math]::Min($gaps.Count-1,[int]($gaps.Count*0.99))]
   $times=@($displayed|ForEach-Object {[double]::Parse($_.TimeInMs,$culture)+[double]::Parse($_.MsUntilDisplayed,$culture)}|Sort-Object)
   $displayFps=($times.Count-1)*1000/($times[-1]-$times[0])
   Record 'display-trace' @{Presents=$steady.Count;Displayed=$displayed.Count;DisplayedFraction=$fraction;DisplayedFramesPerSecond=$displayFps;P99DisplayGapMs=$p99;MaximumDisplayGapMs=$maximum;Runtime=@($steady.PresentRuntime|Select-Object -Unique);PresentMode=@($steady.PresentMode|Select-Object -Unique)}
   Assert ($fraction -ge 0.8 -and $maximum -lt 100) 'Display trace shows dropped/backlogged presentation despite internal frame callbacks.'
   if($MinimumDisplayedFps -gt 0){Assert ($displayFps -ge $MinimumDisplayedFps -and $fraction -ge 0.98 -and $p99 -lt 25 -and $maximum -lt 50) 'Displayed FPS/cadence did not satisfy smooth 60 FPS acceptance.'}
  }catch{$receipt.Passed=$false;$receipt.Error=$_.Exception.Message;$failure=$_}
 }
 $receipt.ProjectUnchanged=(Get-FileHash $project).Hash -eq $before;if(-not $receipt.ProjectUnchanged){$receipt.Passed=$false};Write-Receipt
}
if($failure){throw $failure}
