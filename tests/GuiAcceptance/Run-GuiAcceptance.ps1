[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ActualExePath,
    [Parameter(Mandatory)][string]$MarioRoot,
    [Parameter(Mandatory)][string]$Output
)

# Real desktop input: run on an idle, unlocked English Windows desktop at 100% scaling.
# No product assemblies are loaded into this runner and no replacement game assets are generated.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
$exe = (Resolve-Path -LiteralPath $ActualExePath).Path
$mario = (Resolve-Path -LiteralPath $MarioRoot).Path
$out = [IO.Path]::GetFullPath($Output)
if ([IO.Path]::GetExtension($exe) -ne '.exe') { throw 'ActualExePath must identify the built application executable.' }
if (Test-Path -LiteralPath $out) {
    if (-not (Test-Path -LiteralPath $out -PathType Container) -or @(Get-ChildItem -LiteralPath $out -Force).Count) {
        throw 'Output must be a new or empty directory.'
    }
}
[IO.Directory]::CreateDirectory($out) | Out-Null
$sources = @('FitMario00.pac','FitMarioMotionEtc.pac')
$before = [ordered]@{}
foreach ($name in $sources) { $before[$name] = (Get-FileHash -LiteralPath (Join-Path $mario $name) -Algorithm SHA256).Hash }
$receipt = [ordered]@{Passed=$false;Executable=$exe;ExecutableSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;
    SourcesBefore=$before;SourcesAfter=[ordered]@{};SourcesUnchanged=$false;Steps=[Collections.Generic.List[object]]::new();Error=$null}
$receiptPath = Join-Path $out 'gui-acceptance.json'
function Write-Receipt { $receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receiptPath -Encoding UTF8 }
function Record([string]$Name, $Data) { $receipt.Steps.Add([ordered]@{Name=$Name;Utc=[DateTime]::UtcNow.ToString('o');Data=$Data}); Write-Receipt }

$failure = $null; $fighter = $null; $script:app = $null
try {
    $script:app = Launch-App
    $main = Wait-Until { App-Windows $app.Id | Where-Object { -not $_.Current.IsOffscreen -and $_.Current.ControlType -eq $types::Window } | Select-Object -First 1 } 'application window'
    Invoke-FighterMenu $main @('New Fighter')
    $fighter = Wait-Until { Fighter-Window $app.Id } 'new fighter'
    Assert-Layout $fighter
    # ToolStripTextBox has a dynamic numeric UIA ID; its accessible name is the current text.
    $nameEdit = Wait-Until { Find-Control $fighter -Name 'Fighter name' -Type $types::Edit } 'fighter name Edit'
    Assert ($null -ne $nameEdit) 'The New Fighter name Edit was not exposed.'
    $nameEdit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Mario GUI Acceptance')
    Record 'new-fighter' @{ProcessId=$app.Id;ScreenshotSha256=(Capture $fighter '01-new-fighter.png')}
    Click-Toolbar $fighter 259
    Set-DialogPath $app.Id 'Open Model' (Join-Path $mario 'FitMario00.pac') 'Open'
    Wait-Until { (Control $fighter 'trbFrame').Current.IsEnabled -or (Control $fighter 'modelPanel').Current.BoundingRectangle.Width -gt 300 } 'model viewport' | Out-Null
    $project = Join-Path $out 'Mario.fpstudio'
    Save-Project $fighter $project
    [xml]$modelOnly = Get-Content -LiteralPath $project -Raw
    Assert ($null -ne $modelOnly.FighterPortStudio.Model -and @($modelOnly.SelectNodes('//Animations/Animation')).Count -eq 0) 'Model-only project was not saved correctly.'
    Copy-Item -LiteralPath $project -Destination (Join-Path $out 'model-only.snapshot.xml')
    Record 'model-only-saved' @{ScreenshotSha256=(Capture $fighter '02-model-only.png');ModelNodePath=[string]$modelOnly.FighterPortStudio.ModelNodePath}
    Invoke-Control (Control $fighter 'btnLoad')
    Set-DialogPath $app.Id 'Open' (Join-Path $mario 'FitMarioMotionEtc.pac') 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 297 } '297 Mario animations' | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'Wait1') 'The first motion was not selected as Wait1.'
    Record 'separate-animations-listed' @{Count=297;Selection=(Selected-Clip $fighter);ScreenshotSha256=(Capture $fighter '03-wait1.png')}
    Select-Clip $fighter 'Attack11'; Set-Frame $fighter 10
    Record 'attack-frame10' @{Frame=(Frame $fighter);ScreenshotSha256=(Capture $fighter '04-attack10.png')}
    $observed = [Collections.Generic.HashSet[int]]::new()
    [void]$observed.Add([int](Frame $fighter))
    Invoke-Control (Control $fighter 'btnPlay')
    Wait-Until { (Frame $fighter) -ne 10 } 'playhead advancement' | Out-Null
    1..8 | ForEach-Object { [void]$observed.Add([int](Frame $fighter)); Start-Sleep -Milliseconds 70 }
    Assert ($observed.Count -gt 1) 'The playhead did not advance visibly.'
    if ((Playback-Text $fighter) -eq 'Stop') { Invoke-Control (Control $fighter 'btnPlay') }
    Wait-Until { (Playback-Text $fighter) -eq 'Play' } 'playback to stop' | Out-Null
    Set-Frame $fighter 12
    $attackHash = Capture $fighter '05-attack12.viewport.png' -Viewport
    Save-Project $fighter
    [xml]$saved = Get-Content -LiteralPath $project -Raw
    Assert ([int]$saved.FighterPortStudio.Frame -eq 12 -and [string]$saved.FighterPortStudio.SelectedAnimation -match '/Attack11$') 'The saved selection/frame did not match the GUI.'
    $firstId = $app.Id
    Record 'playback-and-save' @{ObservedFrames=@($observed);SavedFrame=12;ViewportSha256=$attackHash;ScreenshotSha256=(Capture $fighter '06-saved-attack12.png')}
    Close-App $app $fighter
    $script:app = Launch-App $project
    $fighter = Wait-Until { Fighter-Window $app.Id } 'restarted fighter'
    Assert-Layout $fighter
    Assert ($app.Id -ne $firstId) 'The application did not restart in a fresh process.'
    Assert ((Selected-Clip $fighter) -eq 'Attack11' -and (Frame $fighter) -eq 12 -and (Playback-Text $fighter) -eq 'Play') 'Restart did not restore Attack11 at frame12 paused.'
    Start-Sleep -Milliseconds 350
    Assert ((Frame $fighter) -eq 12) 'The restarted playhead was moving.'
    Record 'fresh-process-restore' @{ProcessId=$app.Id;Selection=(Selected-Clip $fighter);Frame=(Frame $fighter);Paused=$true;ScreenshotSha256=(Capture $fighter '07-restarted.png')}
    Select-Clip $fighter 'Run'; Set-Frame $fighter 12
    $runHash = Capture $fighter '08-run12.viewport.png' -Viewport
    Assert ($runHash -ne $attackHash) 'Run and Attack11 produced identical viewport captures.'
    Record 'different-visible-pose' @{Selection='Run';Frame=12;ViewportSha256=$runHash;DiffersFromAttack=$true;ScreenshotSha256=(Capture $fighter '09-run12.png')}
    Select-Clip $fighter 'Attack11'; Set-Frame $fighter 12
    $nativeOutput = Join-Path $out 'Attack11.chr0'
    Click-Toolbar $fighter 607
    Set-DialogPath $app.Id 'Export New Native Output' $nativeOutput 'Save'
    Wait-Until { Test-Path -LiteralPath $nativeOutput -PathType Leaf } 'native CHR0 output' | Out-Null
    Invoke-Control (Control $fighter 'btnLoad'); Set-DialogPath $app.Id 'Open' $nativeOutput 'Open'
    Wait-Until { @(Clip-Rows $fighter).Count -eq 298 } 'reimported native clip' | Out-Null
    Assert ((Selected-Clip $fighter) -eq 'Attack11') 'The separately reimported clip was not selected.'
    Set-Frame $fighter 12; Save-Project $fighter
    Record 'native-export-reimport' @{Count=298;Frame=(Frame $fighter);NativeSha256=(Get-FileHash -LiteralPath $nativeOutput -Algorithm SHA256).Hash;ScreenshotSha256=(Capture $fighter '10-reimported-native.png')}
    Close-App $app $fighter
    $receipt.Passed = $true
} catch {
    $failure = $_; $receipt.Error = $_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace
    try { if ($fighter) { Capture $fighter 'failure.png' | Out-Null } } catch { }
} finally {
    foreach ($name in $sources) {
        try { $receipt.SourcesAfter[$name] = (Get-FileHash -LiteralPath (Join-Path $mario $name) -Algorithm SHA256).Hash }
        catch { $receipt.SourcesAfter[$name] = 'MISSING_OR_UNREADABLE' }
    }
    $receipt.SourcesUnchanged = @($sources | Where-Object { $before[$_] -ne $receipt.SourcesAfter[$_] }).Count -eq 0
    if (-not $receipt.SourcesUnchanged) { $receipt.Passed = $false }
    Write-Receipt
}
if (-not $receipt.SourcesUnchanged) { throw "Original source hashes changed. Inspect $receiptPath" }
if ($failure) { throw $failure }
Write-Output $receiptPath
