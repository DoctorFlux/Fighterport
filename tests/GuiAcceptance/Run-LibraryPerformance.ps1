[CmdletBinding()]
param([Parameter(Mandatory)][string]$ActualExePath,[Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$LibraryRoot,[Parameter(Mandatory)][string]$Output,[string]$Clip='Wait1',[switch]$PlaybackOnly)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
$exe=(Resolve-Path $ActualExePath).Path;$project=(Resolve-Path $ProjectPath).Path;$out=[IO.Path]::GetFullPath($Output)
if((Test-Path $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw 'Choose empty output.'}
[IO.Directory]::CreateDirectory($out)|Out-Null
$env:FIGHTERPORT_LIBRARY_ROOT=(Resolve-Path $LibraryRoot).Path;$beforeProject=(Get-FileHash $project).Hash
$receipt=[ordered]@{Passed=$false;ExecutableSha256=(Get-FileHash $exe).Hash;ProjectUnchanged=$false;Steps=[Collections.Generic.List[object]]::new();Error=$null}
function Write-Receipt {$receipt|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'library-performance.json') -Encoding UTF8}
function Record([string]$Name,$Data){$receipt.Steps.Add(@{Name=$Name;Data=$Data});Write-Receipt}
function Sample($Window,[string]$Phase,[switch]$Search){
    Pause-Playback $Window;Set-Frame $Window 1
    $button=[IntPtr](Control $Window 'btnPlay').Current.NativeWindowHandle
    $target=[IntPtr](Control $Window 'modelPanel').Current.NativeWindowHandle
    $editorHandle=[IntPtr]$Window.Current.NativeWindowHandle
    [FighterGuiAcceptanceNative]::SendMessage($editorHandle,0x84E1,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
    $duration=[FighterGuiAcceptanceNative]::SendMessage([IntPtr](Control $Window 'trbFrame').Current.NativeWindowHandle,0x402,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
    $cameraBefore=@(for($axis=0;$axis -lt 9;$axis++){[FighterGuiAcceptanceNative]::SendMessage($target,0x84F6,[IntPtr]$axis,[IntPtr]::Zero).ToInt64()})
    $source=Find-Control $Window -Id 'libraryOriginalViewport';$sourceHandle=if($source){[IntPtr]$source.Current.NativeWindowHandle}else{[IntPtr]::Zero}
    [FighterGuiAcceptanceNative]::PostMessage($button,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 400
    $firstSource=if($sourceHandle -ne [IntPtr]::Zero){[FighterGuiAcceptanceNative]::SendMessage($sourceHandle,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()}else{0}
    [FighterGuiAcceptanceNative]::SendMessage($target,0x84F1,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
    $first=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
    $app.Refresh();$cpu=$app.TotalProcessorTime.TotalMilliseconds;$clock=[Diagnostics.Stopwatch]::StartNew()
    if($Search){
        Invoke-Control (Control $Window 'libraryClearSearch');Start-Sleep -Milliseconds 650
        $character=Wait-Until {if((Control $Window 'libraryPage').Current.Name -notmatch 'Searching'){Find-Control (Control $Window 'libraryClips') -Name 'mario' -Type $types::TreeItem}} 'complete broad-search outline'
        $character.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        $modelRow=Wait-Until {$character.FindAll($scope::Descendants,[System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::TreeItem))|Where-Object {$_.Current.Name.StartsWith('FitMario00')}|Select-Object -First 1} 'expanded source models'
        $modelRow.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 650
        Type-OwnedEdit (Control $Window 'librarySearch') 'attack';Start-Sleep -Milliseconds 650;Type-OwnedEdit (Control $Window 'librarySearch') 'mario Wait1'
    }
    Start-Sleep -Milliseconds 4000
    $frames=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()-$first
    $seconds=$clock.Elapsed.TotalSeconds;$app.Refresh();$cpu=($app.TotalProcessorTime.TotalMilliseconds-$cpu)/$seconds
    $gap=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F2,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()/1000.0
    $sourceFrames=if($sourceHandle -ne [IntPtr]::Zero){[FighterGuiAcceptanceNative]::SendMessage($sourceHandle,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()-$firstSource}else{0}
    $cameraAfter=@(for($axis=0;$axis -lt 9;$axis++){[FighterGuiAcceptanceNative]::SendMessage($target,0x84F6,[IntPtr]$axis,[IntPtr]::Zero).ToInt64()})
    $operations=@('request','clear','outline','expand-character','append-animations','end-update','undo-state','selection')
    $uiCosts=@(for($i=0;$i -lt $operations.Count;$i++){@{Operation=$operations[$i];MaximumMs=[FighterGuiAcceptanceNative]::SendMessage($editorHandle,0x84E0,[IntPtr]$i,[IntPtr]::Zero).ToInt32()/1000.0;Calls=[FighterGuiAcceptanceNative]::SendMessage($editorHandle,0x84E2,[IntPtr]$i,[IntPtr]::Zero).ToInt32()}})
    Record 'library-ui-operation-costs' @{Phase=$Phase;Operations=$uiCosts}
    Assert (($cameraBefore -join ',') -eq ($cameraAfter -join ',')) 'Playback moved the inspection camera.'
    $traceCount=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F3,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
    [FighterGuiAcceptanceNative]::PostMessage($button,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 200
    $trace=@(for($i=0;$i -lt $traceCount;$i++){
        @{Milliseconds=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F4,[IntPtr]$i,[IntPtr]::Zero).ToInt32()/1000.0;
          Frame=[FighterGuiAcceptanceNative]::SendMessage($target,0x84F5,[IntPtr]$i,[IntPtr]::Zero).ToInt32()/1000.0}
    })
    $trace|ConvertTo-Json -Depth 4|Set-Content (Join-Path $out ($Phase+'-pose-trace.json')) -Encoding UTF8
    $gaps=@($trace.Milliseconds|Sort-Object);$repeats=0;$skips=0
    for($i=1;$i -lt $trace.Count;$i++){if($trace[$i].Frame -eq $trace[$i-1].Frame){$repeats++};if($trace[$i].Frame -gt $trace[$i-1].Frame+1.01){$skips++}}
    $p95=if($gaps.Count){$gaps[[Math]::Min($gaps.Count-1,[int]($gaps.Count*0.95))]}else{0}
    $p99=if($gaps.Count){$gaps[[Math]::Min($gaps.Count-1,[int]($gaps.Count*0.99))]}else{0}
    $elapsedMs=0.0;$poseTicks=0.0;$clockErrors=[Collections.Generic.List[double]]::new()
    for($i=1;$i -lt $trace.Count-1;$i++){
        $elapsedMs+=$trace[$i].Milliseconds;$delta=$trace[$i].Frame-$trace[$i-1].Frame
        if($delta -lt -$duration/2){$delta+=$duration}
        $poseTicks+=$delta;$clockErrors.Add($poseTicks/60*1000-$elapsedMs)
    }
    $range=$clockErrors|Measure-Object -Maximum -Minimum;$drift=$range.Maximum-$range.Minimum
    $fps=$frames/$seconds
    Record 'main-viewport-performance' @{Phase=$Phase;Frames=$frames;Seconds=$seconds;FramesPerSecond=$fps;MaximumSwapGapMs=$gap;SourceViewportFrames=$sourceFrames;CpuMillisecondsPerSecond=$cpu;P95SwapMs=$p95;P99SwapMs=$p99;RepeatedPoses=$repeats;AdvancesAboveOneTick=$skips;TraceSamples=$traceCount;CameraUnchanged=$true;AnimationClockDriftRangeMs=$drift}
    Assert ($frames -gt 60 -and $gap -lt 50 -and $p99 -lt 35) 'Main viewport stalled for 50ms or its 99th percentile exceeded 35ms.'
    Assert ($drift -lt 35) 'Animation time falls behind presentation time by more than 35ms.'
    if($Search){Assert (($uiCosts|ForEach-Object {$_.MaximumMs}|Measure-Object -Maximum).Maximum -lt 10) 'Library work monopolized the UI thread for 10ms.'}
    return $fps
}
$failure=$null;$fighter=$null
try{
    $script:app=Launch-App $project;$fighter=Wait-Until {Fighter-Window $app.Id} 'saved native fighter'
    Select-Clip $fighter $Clip
    $loop=(Control $fighter 'chkLoop').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern);if($loop.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On){$loop.Toggle()}
    $closed=Sample $fighter 'closed'
    if(-not $PlaybackOnly){
    $fighter=Wait-Until {Fighter-Window $app.Id} 'live main preview after timing sample'
    $toolbar=[IntPtr](Control $fighter 'fighterToolbar').Current.NativeWindowHandle
    Assert ([FighterGuiAcceptanceNative]::InvokeAccessibleAsync($toolbar,'Library')) 'The owned Library toolbar action was unavailable.'
    Wait-Until {(Control $fighter 'libraryPage').Current.Name -match '\d+ total'} 'full catalogue outline'|Out-Null
    $empty=Sample $fighter 'open-without-source-preview'
    Type-OwnedEdit (Control $fighter 'librarySearch') 'mario Wait1'
    Wait-Until {foreach($row in (Control $fighter 'libraryClips').FindAll($scope::Subtree,[System.Windows.Automation.Condition]::TrueCondition)){try{if(([string]$row.Current.Name) -match '^Wait1 . FitMario00 . Available$'){$row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();return (Control $fighter 'libraryPreviewOriginal').Current.IsEnabled}}catch{}}} 'verified original source'|Out-Null
    Invoke-Control (Control $fighter 'libraryPreviewOriginal');Wait-Until {(Control $fighter 'libraryOriginalLabel').Current.Name.StartsWith('Original: FitMario00')} 'native source preview'|Out-Null
    $source=Sample $fighter 'open-source-paused';$search=Sample $fighter 'clear-and-search-during-target-playback' -Search
    Assert ($empty -ge $closed*0.85 -and $source -ge $closed*0.85 -and $search -ge $closed*0.85) 'Main playback loses more than 15 percent with the library in use.'
    }
    $end=[FighterGuiAcceptanceNative]::SendMessage([IntPtr](Control $fighter 'trbFrame').Current.NativeWindowHandle,0x402,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
    if($loop.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On){$loop.Toggle()}
    Set-Frame $fighter ([Math]::Max(1,$end-3));Invoke-Control (Control $fighter 'btnPlay')
    Wait-Until {(Frame $fighter) -eq $end -and (Playback-Text $fighter) -eq 'Play'} 'native playback stops at last frame'|Out-Null
    $loop.Toggle();Set-Frame $fighter ([Math]::Max(1,$end-1))
    $slider=[IntPtr](Control $fighter 'trbFrame').Current.NativeWindowHandle
    $play=[IntPtr](Control $fighter 'btnPlay').Current.NativeWindowHandle
    $previous=[FighterGuiAcceptanceNative]::SendMessage($slider,0x400,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
    [FighterGuiAcceptanceNative]::PostMessage($play,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
    $wrapped=$false;$watch=[Diagnostics.Stopwatch]::StartNew()
    do{
        Start-Sleep -Milliseconds 8
        $current=[FighterGuiAcceptanceNative]::SendMessage($slider,0x400,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
        if($current -lt $previous){$wrapped=$true;break};$previous=$current
    }while($watch.Elapsed.TotalSeconds -lt 3)
    Pause-Playback $fighter
    Assert $wrapped 'Native playback failed to wrap after restart.'
    Record 'native-playback-end-loop-restart' @{LastFrame=$end;LoopedFrame=$current;ObservedWrap=$true}
    Capture $fighter 'main-library-performance.png'|Out-Null
    Close-App $app $fighter;$receipt.Passed=$true
}catch{$failure=$_;$receipt.Error=$_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace;try{if($fighter){Capture $fighter 'failure.png'|Out-Null;Pause-Playback $fighter;Close-App $app $fighter}}catch{$receipt['CleanupError']=$_.Exception.Message}}
finally{$receipt.ProjectUnchanged=(Get-FileHash $project).Hash -eq $beforeProject;if(-not $receipt.ProjectUnchanged){$receipt.Passed=$false};Write-Receipt}
if($failure){throw $failure}
