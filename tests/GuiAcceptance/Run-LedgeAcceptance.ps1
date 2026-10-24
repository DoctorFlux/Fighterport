[CmdletBinding()]
param([Parameter(Mandatory)][string]$ActualExePath,[Parameter(Mandatory)][string]$MarioRoot,
    [Parameter(Mandatory)][string]$SnakeRoot,[Parameter(Mandatory)][string]$Output,[switch]$ResumeAfterForward,
    [ValidateSet('Quick','Slow')][string]$CliffSpeed='Quick',[ValidateSet('Ledge','Locomotion')][string]$MotionSet='Ledge')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
$exe=(Resolve-Path -LiteralPath $ActualExePath).Path
$mario=(Resolve-Path -LiteralPath $MarioRoot).Path
$snake=(Resolve-Path -LiteralPath $SnakeRoot).Path
$out=[IO.Path]::GetFullPath($Output)
$reviewClips=if($MotionSet -eq 'Locomotion'){@('Run','JumpSquat','JumpF','Fall','LandingAirN')}else{@('Wait1',('CliffClimb'+$CliffSpeed),('CliffAttack'+$CliffSpeed))}
$previous=$null
if ((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count) {
    if(-not $ResumeAfterForward){throw 'Use a new or empty output directory.'}
    $previous=Get-Content -LiteralPath (Join-Path $out 'ledge-gui.json') -Raw | ConvertFrom-Json
    Assert ($previous.ExecutableSha256 -eq (Get-FileHash $exe).Hash -and $previous.SourcesUnchanged) 'Resume requires the identical executable and unchanged originals.'
    $completed=@($previous.Steps | Where-Object {$_.Name -eq 'ledge-retarget-play-export-reopen'})
    Assert ($completed.Count -eq $reviewClips.Count -and @($completed | Where-Object {$_.Data.Target -ne 'Snake'}).Count -eq 0) 'Resume requires every completed forward case in the selected scope.'
    Assert (@($completed | Where-Object {$_.Data.Clip -notin $reviewClips}).Count -eq 0) 'Resume clip scope differs from the completed checkpoint.'
    Assert (@($previous.Steps | Where-Object {$_.Name -eq 'ledge-project-restart' -and $_.Data.Target -eq 'Snake'}).Count -eq 1) 'Resume requires the completed forward project restart.'
    foreach($step in $completed){Assert ((Get-FileHash (Join-Path $out ('Snake-'+$step.Data.Clip+'.chr0'))).Hash -eq $step.Data.NativeSha256) 'A completed native output changed.'}
    Copy-Item -LiteralPath (Join-Path $out 'ledge-gui.json') -Destination (Join-Path $out 'previous-attempt.json')
}
[IO.Directory]::CreateDirectory($out) | Out-Null
$sources=@(Get-ChildItem -LiteralPath $mario,$snake -File -Recurse)
$before=@{}; foreach($source in $sources){$before[$source.FullName]=(Get-FileHash -LiteralPath $source.FullName).Hash}
$receipt=[ordered]@{Passed=$false;ExecutableSha256=(Get-FileHash -LiteralPath $exe).Hash;SourcesUnchanged=$false;Steps=[Collections.Generic.List[object]]::new();Error=$null}
if($previous){foreach($step in $previous.Steps){$receipt.Steps.Add($step)};$receipt.PreviousAttemptError=$previous.Error}
function Write-Receipt {$receipt | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $out 'ledge-gui.json') -Encoding UTF8}
function Record([string]$Name,$Data){$receipt.Steps.Add([ordered]@{Name=$Name;Utc=[DateTime]::UtcNow.ToString('o');Data=$Data});Write-Receipt}
function Ground-Angle($Window,[string]$Angle){Start-Sleep -Milliseconds 500;Invoke-FighterMenu $Window @('Ground Contact View',$Angle);Start-Sleep -Milliseconds 500}
function Assert-ActorVisible($Window,[string]$Name) {
    $path=Join-Path $out $Name
    Capture $Window $Name -Viewport | Out-Null
    $bitmap=[Drawing.Bitmap]::new($path)
    try {
        $pixels=0
        for($y=10;$y -lt $bitmap.Height-10;$y+=3){for($x=10;$x -lt $bitmap.Width-10;$x+=3){
            $color=$bitmap.GetPixel($x,$y)
            # The native background/floor are lavender. Count contrasting actor
            # pixels inside the viewport, excluding control borders/text.
            if($color.R -gt $color.B+12 -or $color.G -gt $color.B+8 -or
               ([Math]::Abs([int]$color.R-$color.B) -lt 14 -and [Math]::Abs([int]$color.G-$color.B) -lt 14)){$pixels++}
        }}
        Assert ($pixels -gt 100) 'The ground-level view lost the actor.'
        return $pixels
    } finally {$bitmap.Dispose()}
}
$failure=$null; $fighter=$null; $script:app=$null
try {
    $directions=@(@{Source='Mario';Target='Snake';SourceRoot=$mario;TargetRoot=$snake;Count=297},
                 @{Source='Snake';Target='Mario';SourceRoot=$snake;TargetRoot=$mario;Count=355})
    if($previous){$directions=@($directions[1]);Record 'resume-after-forward-project-restart' @{Reason='Owned unsaved-preview close prompt; retained verified forward captures and native outputs.'}}
    foreach($direction in $directions) {
        $script:app=Launch-App
        $main=Wait-Until {App-Windows $app.Id | Where-Object {$_.Current.AutomationId -eq 'MainForm'} | Select-Object -First 1} 'main window'
        Invoke-FighterMenu $main @('New Fighter')
        $fighter=Wait-Until {Fighter-Window $app.Id} 'new ledge fighter'
        Click-Toolbar $fighter 259
        Set-DialogPath $app.Id 'Open Model' (Join-Path $direction.TargetRoot ('Fit'+$direction.Target+'00.pac')) 'Open'
        Invoke-Control (Control $fighter 'btnLoad')
        Set-DialogPath $app.Id 'Open' (Join-Path $direction.SourceRoot ('Fit'+$direction.Source+'MotionEtc.pac')) 'Open'
        $count=$direction.Count
        Wait-Until {@(Clip-Rows $fighter).Count -eq $count} 'donor ledge clips' | Out-Null
        $first=$true
        foreach($clip in $reviewClips) {
            Select-Clip $fighter $clip; Set-Frame $fighter 1
            Click-Toolbar $fighter 805
            if($first){Set-DialogPath $app.Id 'Choose Animation Source Model' (Join-Path $direction.SourceRoot ('Fit'+$direction.Source+'00.pac')) 'Open';$first=$false}
            $count++
            Wait-Until {@(Clip-Rows $fighter).Count -eq $count} ($direction.Target+' '+$clip+' retarget') 1200 | Out-Null
            Record 'retarget-ready' @{Target=$direction.Target;Clip=$clip}
            Select-Clip $fighter ($clip+' → Fit'+$direction.Target+'00')
            $last=[FighterGuiAcceptanceNative]::SendMessage([IntPtr](Control $fighter 'trbFrame').Current.NativeWindowHandle,0x402,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
            $middle=[Math]::Max(1,[int][Math]::Ceiling($last/2))
            $frames=@()
            Ground-Angle $fighter 'Three-quarter'
            $sampleFrames=@(1,[int][Math]::Ceiling($last/4),$middle,[int][Math]::Ceiling($last*3/4),$last) | Select-Object -Unique
            foreach($frame in $sampleFrames) {
                Set-Frame $fighter $frame
                $frames+=@{Frame=$frame;Sha256=(Capture $fighter ($direction.Target+'-'+$clip+'-'+$frame+'.png'))}
            }
            Assert ($frames[0].Sha256 -ne $frames[1].Sha256) 'Ledge poses did not change visibly.'
            Set-Frame $fighter $last
            Ground-Angle $fighter 'Low side'
            $feet=Capture $fighter ($direction.Target+'-'+$clip+'-feet.png')
            $actorPixels=Assert-ActorVisible $fighter ($direction.Target+'-'+$clip+'-feet-viewport.png')
            $footViews=@()
            foreach($frame in $sampleFrames){
                Set-Frame $fighter $frame
                Ground-Angle $fighter 'Low side'
                $footViews+=@{Frame=$frame;Sha256=(Capture $fighter ($direction.Target+'-'+$clip+'-feet-'+$frame+'.png'))}
            }
            Set-Frame $fighter $middle
            Ground-Angle $fighter 'Opposite side'
            $opposite=Capture $fighter ($direction.Target+'-'+$clip+'-opposite.png')
            Record 'whole-motion-camera-captures' @{Target=$direction.Target;Clip=$clip;FootViews=$footViews}
            Set-Frame $fighter 1
            $loop=Control $fighter 'chkLoop'
            $toggle=$loop.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            if($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off){$toggle.Toggle()}
            Record 'whole-clip-playback-started' @{Target=$direction.Target;Clip=$clip;LastFrame=$last}
            Invoke-Control (Control $fighter 'btnPlay')
            Wait-Until {(Frame $fighter) -gt 1} 'ledge native playback started' | Out-Null
            Wait-Until {(Frame $fighter) -eq $last -and (Playback-Text $fighter) -eq 'Play'} 'whole native clip playback completed' 60 | Out-Null
            Record 'whole-clip-playback-completed' @{Target=$direction.Target;Clip=$clip;LastFrame=$last}
            Pause-Playback $fighter; Set-Frame $fighter $middle
            $native=Join-Path $out ($direction.Target+'-'+$clip+'.chr0')
            Click-Toolbar $fighter 607
            Set-DialogPath $app.Id 'Export New Native Output' $native 'Save'
            Wait-Until {Test-Path -LiteralPath $native} 'ledge native export' | Out-Null
            Invoke-Control (Control $fighter 'btnLoad'); Set-DialogPath $app.Id 'Open' $native 'Open'
            $count++
            Wait-Until {@(Clip-Rows $fighter).Count -eq $count} 'independent ledge CHR0 reopen' | Out-Null
            Assert ((Selected-Clip $fighter) -eq $clip) 'Freshly imported ledge output was not selected.'
            Set-Frame $fighter $middle
            Record 'ledge-retarget-play-export-reopen' @{Source=$direction.Source;Target=$direction.Target;Clip=$clip;Count=$count;Views=$frames;FootViews=$footViews;WholePlaybackCompleted=$true;Feet=$feet;ActorPixels=$actorPixels;Opposite=$opposite;NativeSha256=(Get-FileHash -LiteralPath $native).Hash}
        }
        $project=Join-Path $out ($direction.Target+'-Ledge.fpstudio')
        Save-Project $fighter $project
        Close-App $app $fighter
        $script:app=Launch-App $project
        $fighter=Wait-Until {Fighter-Window $app.Id} 'ledge project restart'
        Wait-Until {@(Clip-Rows $fighter).Count -eq $count} 'restored ledge clips' | Out-Null
        Set-Frame $fighter 1
        Record 'ledge-project-restart' @{Target=$direction.Target;Count=$count;ScreenshotSha256=(Capture $fighter ($direction.Target+'-restart.png'))}
        Close-App $app $fighter
    }
    $receipt.Passed=$true
} catch {
    $failure=$_; $receipt.Error=$_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace
    try {if($fighter){Capture $fighter 'failure.png' | Out-Null}}catch{}
    if($script:app){
        try {Record 'failure-process-state' @{Exited=$script:app.HasExited;Windows=@(App-Windows $script:app.Id | ForEach-Object {
            $window=$_;if(-not $window.Current.IsOffscreen){try{Capture $window ('failure-window-'+$window.Current.NativeWindowHandle+'.png')|Out-Null}catch{}}
            @{Name=$window.Current.Name;Id=$window.Current.AutomationId;Offscreen=$window.Current.IsOffscreen}
        })}}catch{}
    }
} finally {
    $receipt.SourcesUnchanged=@($sources | Where-Object {$before[$_.FullName] -ne (Get-FileHash -LiteralPath $_.FullName).Hash}).Count -eq 0
    if(-not $receipt.SourcesUnchanged){$receipt.Passed=$false}
    Write-Receipt
}
if($failure){throw $failure}
if(-not $receipt.SourcesUnchanged){throw 'Original source hashes changed.'}
