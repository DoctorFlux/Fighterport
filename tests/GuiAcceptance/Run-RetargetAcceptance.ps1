[CmdletBinding()]
param([Parameter(Mandatory)][string]$ActualExePath,[Parameter(Mandatory)][string]$MarioRoot,
    [Parameter(Mandatory)][string]$SnakeRoot,[Parameter(Mandatory)][string]$Output)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
$exe = (Resolve-Path -LiteralPath $ActualExePath).Path
$mario = (Resolve-Path -LiteralPath $MarioRoot).Path
$snake = (Resolve-Path -LiteralPath $SnakeRoot).Path
$out = [IO.Path]::GetFullPath($Output)
if ((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count) { throw 'Use a new or empty output directory.' }
[IO.Directory]::CreateDirectory($out) | Out-Null
$sources = @(Get-ChildItem -LiteralPath $mario,$snake -File -Recurse)
$before = [ordered]@{}
foreach ($source in $sources) { $before[$source.FullName] = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash }
$receipt = [ordered]@{Passed=$false;Executable=$exe;ExecutableSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;
    SourcesBefore=$before;SourcesAfter=[ordered]@{};SourcesUnchanged=$false;Steps=[Collections.Generic.List[object]]::new();Error=$null}
$receiptPath = Join-Path $out 'retarget-gui.json'
function Write-Receipt { $receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding UTF8 }
function Record([string]$Name, $Data) { $receipt.Steps.Add([ordered]@{Name=$Name;Utc=[DateTime]::UtcNow.ToString('o');Data=$Data}); Write-Receipt }
function Retarget($Window) {
    Click-Toolbar $Window 805
}
function Ground-Angle($Window,[string]$Angle) {
    Invoke-FighterMenu $Window @('Ground Contact View',$Angle)
    Start-Sleep -Milliseconds 500
}
function Ground-Mode([int]$Index) {
    Invoke-FighterMenu (Fighter-Window $script:app.Id) @('Retarget Options...')
    $dialog=Wait-Until {App-Windows $script:app.Id | Where-Object {$_.Current.Name -eq 'Retarget Options'}} 'retarget options dialog'
    $combo=Find-Control $dialog -Id 'retargetGroundMode'
    [FighterGuiAcceptanceNative]::SendMessage([IntPtr]$combo.Current.NativeWindowHandle,0x14E,[IntPtr]$Index,[IntPtr]::Zero) | Out-Null
    [FighterGuiAcceptanceNative]::SendMessage([IntPtr]$combo.Current.NativeWindowHandle,0x14F,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
    Assert ([FighterGuiAcceptanceNative]::SendMessage([IntPtr]$combo.Current.NativeWindowHandle,0x147,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32() -eq $Index) 'Ground mode selection did not change.'
    Invoke-Control (Find-Control $dialog -Name 'Apply' -Type $types::Button)
    Wait-Until {-not (App-Windows $script:app.Id | Where-Object {$_.Current.Name -eq 'Retarget Options'})} 'options applied' | Out-Null
}
function Idle-Cases($Window,[string]$Target,[int]$InitialCount) {
    $count=$InitialCount
    foreach($clip in @('Wait1','Wait2','Wait3')) {
        $existing=Clip-Rows $Window | Where-Object {$_.Current.Name -eq ($clip+' → '+$Target)} | Select-Object -First 1
        if($existing) { Select-Clip $Window ($clip+' → '+$Target) }
        else {
            Select-Clip $Window $clip; Set-Frame $Window 1
            Retarget $Window; $count++
            Wait-Until { @(Clip-Rows $Window).Count -eq $count } ($Target+' idle '+$clip) 1200 | Out-Null
        }
        Assert ((Selected-Clip $Window) -eq ($clip+' → '+$Target)) 'Idle retarget was not selected.'
        Ground-Angle $Window 'Low side'
        $last=[FighterGuiAcceptanceNative]::SendMessage([IntPtr](Control $Window 'trbFrame').Current.NativeWindowHandle,0x402,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
        $middle=[Math]::Max(1,[int][Math]::Ceiling($last/2))
        $views=@()
        foreach($frame in @(1,$middle,$last)) {
            Set-Frame $Window $frame
            $views+=@{Frame=$frame;Sha256=(Capture $Window ($Target+'-'+$clip+'-low-side-'+$frame+'.png'))}
        }
        Set-Frame $Window $middle
        Ground-Angle $Window 'Opposite side'
        $opposite=Capture $Window ($Target+'-'+$clip+'-opposite.png')
        Invoke-Control (Control $Window 'btnPlay')
        Wait-Until { (Frame $Window) -ne $middle } ($clip+' native idle playback') | Out-Null
        if ((Playback-Text $Window) -eq 'Stop') {Invoke-Control (Control $Window 'btnPlay')}
        Wait-Until {(Playback-Text $Window) -eq 'Play'} 'idle playback stopped' | Out-Null
        Set-Frame $Window 1
        Ground-Angle $Window 'Three-quarter'
        Record 'idle-ground-views' @{Target=$Target;Clip=$clip;Count=$count;LowSideFrames=$views;OppositeSideSha256=$opposite;ScreenshotSha256=(Capture $Window ($Target+'-'+$clip+'-three-quarter.png'))}
    }
}
$failure=$null; $fighter=$null; $script:app=$null
try {
    $script:app=Launch-App
    $main=Wait-Until {App-Windows $app.Id | Where-Object {$_.Current.AutomationId -eq 'MainForm'} | Select-Object -First 1} 'main window'
    Invoke-FighterMenu $main @('New Fighter')
    $fighter=Wait-Until { Fighter-Window $app.Id } 'new fighter'
    $name=Wait-Until { Find-Control $fighter -Name 'Fighter name' -Type $types::Edit } 'fighter name Edit'
    $name.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Snake Retarget Acceptance')
    Record 'new-fighter' @{ProcessId=$app.Id}
    Click-Toolbar $fighter 259
    Set-DialogPath $app.Id 'Open Model' (Join-Path $snake 'FitSnake00.pac') 'Open'
    Record 'target-model-loaded' @{ScreenshotSha256=(Capture $fighter '00-snake-model.png')}
    Invoke-FighterMenu $fighter @('Review Skeleton Mapping...')
    $mapping=Wait-Until {App-Windows $app.Id | Where-Object {$_.Current.Name -like 'Skeleton Mapping*'}} 'skeleton mapping dialog'
    Invoke-Control (Control $mapping 'applySkeletonMap')
    Wait-Until {-not (App-Windows $app.Id | Where-Object {$_.Current.Name -like 'Skeleton Mapping*'})} 'validated skeleton mapping' | Out-Null
    Record 'automatic-skeleton-review' @{ScreenshotSha256=(Capture $fighter '00a-skeleton-reviewed.png')}

    Invoke-Control (Control $fighter 'btnLoad')
    Set-DialogPath $app.Id 'Open' (Join-Path $mario 'FitMarioMotionEtc.pac') 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 297 } 'Mario motion list' | Out-Null
    Record 'source-motions-loaded' @{Count=297}
    Select-Clip $fighter 'Wait1'; Set-Frame $fighter 10
    Retarget $fighter
    Set-DialogPath $app.Id 'Choose Animation Source Model' (Join-Path $mario 'FitMario00.pac') 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 298 } 'baked native motion' 60 | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'Wait1 → FitSnake00') 'The retargeted motion was not selected and labelled for Snake.'
    $floor=Wait-Until { Find-Control $fighter -Name 'Floor' -Type $types::Button } 'native floor button'
    Assert (($floor.GetNativePattern(10018).Current.LegacyState -band 0x10) -ne 0) 'Grounded retarget did not enable the native floor.'
    Assert-Layout $fighter
    Ground-Angle $fighter 'Low side'
    $frame10=Capture $fighter '01-retargeted-idle10.png'
    Set-Frame $fighter 3
    $frame3=Capture $fighter '02-retargeted-idle3.viewport.png' -Viewport
    Set-Frame $fighter 12
    $frame12=Capture $fighter '03-retargeted-idle12.viewport.png' -Viewport
    Assert ($frame3 -ne $frame12) 'Scrubbing did not change the rendered target pose.'
    $observed=[Collections.Generic.HashSet[int]]::new(); [void]$observed.Add(12)
    Invoke-Control (Control $fighter 'btnPlay')
    Wait-Until { (Frame $fighter) -ne 12 } 'retarget playback' | Out-Null
    1..8 | ForEach-Object { [void]$observed.Add([int](Frame $fighter)); Start-Sleep -Milliseconds 70 }
    if ((Playback-Text $fighter) -eq 'Stop') { Invoke-Control (Control $fighter 'btnPlay') }
    Assert ($observed.Count -gt 1) 'Retarget playback did not advance.'
    Set-Frame $fighter 12
    $project=Join-Path $out 'Snake.fpstudio'
    Save-Project $fighter $project
    [xml]$saved=Get-Content -LiteralPath $project -Raw
    $generated=@($saved.SelectNodes('//Animations/Animation[@Generated="true"]'))
    Assert ($generated.Count -eq 1) 'The saved project lost its generated asset.'
    $p=$generated[0].Retarget
    Assert ($p.FromProfile -eq 'FitMario00' -and $p.ToProfile -eq 'FitSnake00' -and @($p.Mapping.Bone).Count -eq 47) 'Saved provenance does not describe the reviewed map.'
    Assert (@($p.Mapping.Bone | Where-Object { $_.Source -eq 'WaistN' -and $_.Target -eq 'BodyN' }).Count -eq 1) 'Spine mapping is missing.'
    Assert ([double]$p.LegScale -gt 2 -and [double]$p.ArmScale -gt 1.4) 'Measured body proportions were not retained.'
    Record 'retarget-scrub-play-save' @{Frame=12;ObservedFrames=@($observed);Mapping=47;LegScale=$p.LegScale;ArmScale=$p.ArmScale;ScreenshotSha256=$frame10}
    $first=$app.Id; Close-App $app $fighter
    $script:app=Launch-App $project
    $fighter=Wait-Until { Fighter-Window $app.Id } 'restarted retarget project'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 298 } 'restored motion list' | Out-Null
    Assert ($app.Id -ne $first -and (Selected-Clip $fighter) -eq 'Wait1 → FitSnake00' -and (Frame $fighter) -eq 12) 'Fresh process did not restore the retargeted motion and frame.'
    Assert ((Playback-Text $fighter) -eq 'Play') 'Restored animation was not paused.'
    Record 'fresh-process-retarget-restore' @{ProcessId=$app.Id;ScreenshotSha256=(Capture $fighter '04-restarted.png')}
    $native=Join-Path $out 'Wait1-Snake.chr0'
    Click-Toolbar $fighter 607
    Set-DialogPath $app.Id 'Export New Native Output' $native 'Save'
    Wait-Until { Test-Path -LiteralPath $native } 'exported CHR0' | Out-Null
    Invoke-Control (Control $fighter 'btnLoad'); Set-DialogPath $app.Id 'Open' $native 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 299 } 'native output independently reopened' | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'Wait1') 'The new independent CHR0 was not selected.'
    Set-Frame $fighter 12
    Record 'native-chr0-export-and-reopen' @{NativeSha256=(Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash;ScreenshotSha256=(Capture $fighter '05-reopened-native.png')}
    Idle-Cases $fighter 'FitSnake00' 299
    # The original donor stays available. Reuse its saved source skeleton for a second clip.
    Select-Clip $fighter 'Run'; Set-Frame $fighter 12
    Retarget $fighter
    Wait-Until { @(Clip-Rows $fighter).Count -eq 302 } 'cached-source Run retarget' 60 | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'Run → FitSnake00') 'Second retarget did not use the stored source skeleton.'
    Set-Frame $fighter 5; $run5=Capture $fighter '06-run5.viewport.png' -Viewport
    Set-Frame $fighter 15; $run15=Capture $fighter '07-run15.viewport.png' -Viewport
    Assert ($run5 -ne $run15) 'Retargeted Run did not animate visibly.'
    Record 'second-clip-and-root-follow' @{ScreenshotSha256=(Capture $fighter '08-retargeted-run.png')}
    Click-Toolbar $fighter 653
    Wait-Until { @(Clip-Rows $fighter).Count -eq 301 } 'undo generated Run only' | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'Run') 'Undo did not restore the original donor clip.'
    Retarget $fighter
    Wait-Until { @(Clip-Rows $fighter).Count -eq 302 } 'retarget after undo' 60 | Out-Null
    Record 'undo-and-retarget' @{Count=302}
    Select-Clip $fighter 'DownStandU'; Set-Frame $fighter 11
    Retarget $fighter
    Wait-Until { @(Clip-Rows $fighter).Count -eq 303 } 'hand-supported get-up retarget' 60 | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'DownStandU → FitSnake00') 'The get-up retarget was not selected.'
    Set-Frame $fighter 5; $hand5=Capture $fighter '09-hand5.viewport.png' -Viewport
    Set-Frame $fighter 11; $hand11=Capture $fighter '10-hand11.viewport.png' -Viewport
    Assert ($hand5 -ne $hand11) 'The hand-supported motion did not change the visible pose.'
    Save-Project $fighter
    [xml]$contacts=Get-Content -LiteralPath $project -Raw
    $handContact=@($contacts.SelectNodes('//Animations/Animation/Retarget') | Where-Object { $_.SourceClip -like 'DownStandU|*' })[0]
    Assert ([int]$handContact.PlantedHandSamples -gt 0 -and [int]$handContact.PlantedFootSamples -gt 0) 'The saved get-up motion lost hand/foot contact diagnostics.'
    Record 'hand-supported-get-up' @{HandSamples=$handContact.PlantedHandSamples;FootSamples=$handContact.PlantedFootSamples;ScreenshotSha256=$hand11}
    foreach($bodyCase in @(@{Clip='SlipWait';Count=304;Frame=56},@{Clip='DownWaitU';Count=305;Frame=36})) {
        Select-Clip $fighter $bodyCase.Clip; Set-Frame $fighter $bodyCase.Frame
        Retarget $fighter
        Wait-Until { @(Clip-Rows $fighter).Count -eq $bodyCase.Count } ('body retarget '+$bodyCase.Clip) 1200 | Out-Null
        Assert ((Selected-Clip $fighter) -eq ($bodyCase.Clip+' → FitSnake00')) 'The body-supported result was not selected.'
        Set-Frame $fighter $bodyCase.Frame
        $shot=Capture $fighter ($bodyCase.Clip+'-Snake-ground.png')
        Save-Project $fighter
        [xml]$bodyProject=Get-Content -LiteralPath $project -Raw
        $bp=@($bodyProject.SelectNodes('//Animations/Animation/Retarget') | Where-Object {$_.SourceClip -like ($bodyCase.Clip+'|*')})[0]
        Assert ([int]$bp.PlantedBodySamples -gt 0) 'The saved seated/lying motion has no body support diagnostics.'
        Record 'body-supported-retarget' @{Clip=$bodyCase.Clip;BodySamples=$bp.PlantedBodySamples;ScreenshotSha256=$shot}
    }
    Ground-Mode 2
    Select-Clip $fighter 'Wait1'; Set-Frame $fighter 12
    Retarget $fighter
    Wait-Until { @(Clip-Rows $fighter).Count -eq 306 } 'explicit airborne retarget' 300 | Out-Null
    Save-Project $fighter
    [xml]$air=Get-Content -LiteralPath $project -Raw
    $ap=@($air.SelectNodes('//Animations/Animation[@Generated="true"]/Retarget'))[-1]
    Assert ($ap.GroundMode -eq 'Airborne' -and $ap.FloorContactsEnabled -eq 'false' -and $air.FighterPortStudio.GroundMode -eq 'Airborne') 'Airborne UI mode was not applied and saved.'
    Record 'explicit-airborne-mode' @{GroundMode=$ap.GroundMode;FloorContacts=$ap.FloorContactsEnabled}
    Click-Toolbar $fighter 653
    Wait-Until { @(Clip-Rows $fighter).Count -eq 305 } 'undo airborne test result' | Out-Null
    Ground-Mode 1
    Ground-Mode 3
    Ground-Mode 0
    Select-Clip $fighter 'Wait1 → FitSnake00'; Set-Frame $fighter 12
    $pac=Join-Path $out 'FitSnakeMotionEtc-retargeted.pac'
    Click-Toolbar $fighter 607
    # A typed explicit PAC extension is honored by SaveFileDialog even with the first filter selected.
    Set-DialogPath $app.Id 'Export New Native Output' $pac 'Save'
    Set-DialogPath $app.Id 'Choose Target Motion PAC Template' (Join-Path $snake 'FitSnakeMotionEtc.pac') 'Open'
    Wait-Until { Test-Path -LiteralPath $pac } 'new native target motion PAC' 60 | Out-Null
    Invoke-Control (Control $fighter 'btnLoad'); Set-DialogPath $app.Id 'Open' $pac 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 660 } '355-slot native target PAC reopened' 60 | Out-Null
    $group=Find-Control (Control $fighter 'listAnims') -Name 'FitSnakeMotionEtc-retargeted.pac' -Type $types::Group
    $row=Find-Control $group -Name 'Wait1' -Type $types::ListItem
    Assert ($null -ne $row) 'The freshly reopened target PAC has no Wait1 row.'
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Set-Frame $fighter 12
    Save-Project $fighter
    Record 'native-target-pac-export-reopen' @{Count=660;NativeSha256=(Get-FileHash -LiteralPath $pac -Algorithm SHA256).Hash;ScreenshotSha256=(Capture $fighter '11-final.png')}
    Close-App $app $fighter
    # Reverse the actual source/target selection in a separate fighter project.
    $script:app=Launch-App
    $main=Wait-Until {App-Windows $app.Id | Where-Object {$_.Current.AutomationId -eq 'MainForm'} | Select-Object -First 1} 'reverse main window'
    Invoke-FighterMenu $main @('New Fighter')
    $fighter=Wait-Until { Fighter-Window $app.Id } 'new reverse fighter'
    Click-Toolbar $fighter 259
    Set-DialogPath $app.Id 'Open Model' (Join-Path $mario 'FitMario00.pac') 'Open'
    Invoke-Control (Control $fighter 'btnLoad')
    Set-DialogPath $app.Id 'Open' (Join-Path $snake 'FitSnakeMotionEtc.pac') 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 355 } 'Snake donor motions' | Out-Null
    Select-Clip $fighter 'DownStandU'; Set-Frame $fighter 5
    Retarget $fighter
    Set-DialogPath $app.Id 'Choose Animation Source Model' (Join-Path $snake 'FitSnake00.pac') 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 356 } 'Snake-to-Mario get-up' 60 | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'DownStandU → FitMario00') 'Reverse retarget did not select the Mario result.'
    Set-Frame $fighter 5; $reverse5=Capture $fighter '12-reverse-hand5.viewport.png' -Viewport
    Set-Frame $fighter 13; $reverse13=Capture $fighter '13-reverse-hand13.viewport.png' -Viewport
    Assert ($reverse5 -ne $reverse13) 'Reverse retarget did not animate visibly.'
    foreach($bodyCase in @(@{Clip='SlipWait';Count=357;Frame=20},@{Clip='DownWaitD';Count=358;Frame=20})) {
        Select-Clip $fighter $bodyCase.Clip; Set-Frame $fighter $bodyCase.Frame
        Retarget $fighter
        Wait-Until { @(Clip-Rows $fighter).Count -eq $bodyCase.Count } ('reverse body retarget '+$bodyCase.Clip) 1200 | Out-Null
        Assert ((Selected-Clip $fighter) -eq ($bodyCase.Clip+' → FitMario00')) 'The reverse body-supported result was not selected.'
        Set-Frame $fighter $bodyCase.Frame
        Record 'reverse-body-supported-retarget' @{Clip=$bodyCase.Clip;ScreenshotSha256=(Capture $fighter ($bodyCase.Clip+'-Mario-ground.png'))}
    }
    Idle-Cases $fighter 'FitMario00' 358
    Select-Clip $fighter 'DownStandU → FitMario00'; Set-Frame $fighter 13
    $reverseProject=Join-Path $out 'Mario.fpstudio'
    Save-Project $fighter $reverseProject
    [xml]$reverse=Get-Content -LiteralPath $reverseProject -Raw
    $rp=@($reverse.SelectNodes('//Animations/Animation[@Generated="true"]/Retarget') | Where-Object {$_.SourceClip -like 'DownStandU|*'})[0]
    Assert ($rp.FromProfile -eq 'FitSnake00' -and $rp.ToProfile -eq 'FitMario00' -and [double]$rp.LegScale -lt 1 -and [int]$rp.PlantedHandSamples -gt 0) 'Reverse contact/proportion provenance is incorrect.'
    Record 'reverse-retarget-scrub-save' @{LegScale=$rp.LegScale;ArmScale=$rp.ArmScale;HandSamples=$rp.PlantedHandSamples;ScreenshotSha256=$reverse13}
    Close-App $app $fighter
    $script:app=Launch-App $reverseProject
    $fighter=Wait-Until { Fighter-Window $app.Id } 'reverse project restart'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 361 } 'reverse project restored clips' | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'DownStandU → FitMario00' -and (Frame $fighter) -eq 13) 'Reverse project did not restore selection/frame.'
    $reverseNative=Join-Path $out 'DownStandU-Mario.chr0'
    Click-Toolbar $fighter 607
    Set-DialogPath $app.Id 'Export New Native Output' $reverseNative 'Save'
    Wait-Until { Test-Path -LiteralPath $reverseNative } 'reverse native output' | Out-Null
    Invoke-Control (Control $fighter 'btnLoad'); Set-DialogPath $app.Id 'Open' $reverseNative 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 362 } 'reverse native independent reopen' | Out-Null
    Set-Frame $fighter 13; Save-Project $fighter
    Record 'reverse-restart-native-reopen' @{NativeSha256=(Get-FileHash -LiteralPath $reverseNative -Algorithm SHA256).Hash;ScreenshotSha256=(Capture $fighter '14-reverse-reopened.png')}
    Close-App $app $fighter
    $receipt.Passed=$true
} catch {
    $failure=$_; $receipt.Error=$_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace
    try { if ($fighter) { Capture $fighter 'failure.png' | Out-Null } } catch { }
} finally {
    foreach ($source in $sources) { $receipt.SourcesAfter[$source.FullName]=(Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash }
    $receipt.SourcesUnchanged=@($sources | Where-Object { $before[$_.FullName] -ne $receipt.SourcesAfter[$_.FullName] }).Count -eq 0
    if (-not $receipt.SourcesUnchanged) { $receipt.Passed=$false }
    Write-Receipt
}
if (-not $receipt.SourcesUnchanged) { throw 'Original source hashes changed.' }
if ($failure) { throw $failure }
Write-Output $receiptPath
