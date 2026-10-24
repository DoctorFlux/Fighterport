[CmdletBinding()]
param([Parameter(Mandatory)][string]$ActualExePath,[Parameter(Mandatory)][string]$SnakeRoot,
    [Parameter(Mandatory)][string]$ImagePath,[Parameter(Mandatory)][string]$DolphinTool,
    [Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$LibraryRoot,[switch]$ExistingCatalogue,[switch]$ResumeAfterRepair)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
Add-Type -AssemblyName System.Xml.Linq
$exe=(Resolve-Path -LiteralPath $ActualExePath).Path;$out=[IO.Path]::GetFullPath($Output)
if((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count -and -not $ResumeAfterRepair){throw 'Use empty output.'}
[IO.Directory]::CreateDirectory($out)|Out-Null
$env:FIGHTERPORT_LIBRARY_ROOT=[IO.Path]::GetFullPath($LibraryRoot)
$image=(Resolve-Path -LiteralPath $ImagePath).Path;$tool=(Resolve-Path -LiteralPath $DolphinTool).Path
$model=Join-Path (Resolve-Path -LiteralPath $SnakeRoot).Path 'FitSnake00.pac'
$imageHash=[FighterGuiAcceptanceNative]::HashSourceFile($image);$modelHash=[FighterGuiAcceptanceNative]::HashSourceFile($model)
$receipt=[ordered]@{Passed=$false;ExecutableSha256=(Get-FileHash $exe).Hash;Steps=[Collections.Generic.List[object]]::new();SourceUnchanged=$false;Error=$null}
if($ResumeAfterRepair){
    $prior=Get-Content -LiteralPath (Join-Path $out 'game-source-gui.json') -Raw|ConvertFrom-Json
    if($prior.ExecutableSha256 -ne $receipt.ExecutableSha256 -or -not $prior.SourceUnchanged -or
        @($prior.Steps|Where-Object Name -eq 'actual-render-performance').Count -ne 2 -or
        @($prior.Steps|Where-Object Name -eq 'disc-retarget-native-roundtrip').Count -ne 1){throw 'Resume needs verified rendering/native roundtrip checkpoints, unchanged sources and the same executable.'}
    $native=Join-Path $out 'Disc-Mario-to-Snake.chr0';$expected=($prior.Steps|Where-Object Name -eq 'disc-retarget-native-roundtrip').Data.OutputSha256
    if((Get-FileHash $native).Hash -ne $expected){throw 'Checkpoint native output changed.'}
    $last=Get-ChildItem -LiteralPath $env:FIGHTERPORT_LIBRARY_ROOT -Filter 'catalogue-*.xml'|Sort-Object Name|Select-Object -Last 1
    [xml]$repaired=Get-Content $last.FullName
    if($repaired.FighterPortAnimationLibrary.LastOperation -ne 'Repair Disc Source' -or -not @($repaired.FighterPortAnimationLibrary.GameLocations.Location|Where-Object {$_.SelectSingleNode('ContainerSha256') -and $_.ContainerSha256 -eq $imageHash.ToLowerInvariant() -and $_.Path -eq $image}).Count){throw 'Resume requires the completed exact-image repair and matching live source hash.'}
    foreach($step in $prior.Steps){$receipt.Steps.Add($step)}
    $receipt['PriorFailure']=$prior.Error
}
function Write-Receipt {$receipt|ConvertTo-Json -Depth 15|Set-Content (Join-Path $out 'game-source-gui.json') -Encoding UTF8}
function Record([string]$Name,$Data){$receipt.Steps.Add(@{Name=$Name;Data=$Data});Write-Receipt}
function Search-Source($Window,[string]$State='Available'){
    Type-OwnedEdit (Control $Window 'librarySearch') 'mario Wait1'
    Wait-Until {
        foreach($item in (Control $Window 'libraryClips').FindAll($scope::Subtree,[System.Windows.Automation.Condition]::TrueCondition)){
            try{if(([string]$item.Current.Name) -match ('^Wait1 . FitMario00 . '+[regex]::Escape($State)+'$')){$item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();return (Control $Window 'libraryRepairSource').Current.IsEnabled}}catch{}
        }
    } ('original Mario source '+$State) 90|Out-Null
}
function Measure-Rendering($Window,[string]$Phase,[switch]$Search){
    Pause-Playback $Window;Set-Frame $Window 1
    $toggle=(Control $Window 'chkLoop').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On){$toggle.Toggle()}
    $handle=[IntPtr](Control $Window 'modelPanel').Current.NativeWindowHandle
    $play=[IntPtr](Control $Window 'btnPlay').Current.NativeWindowHandle
    [FighterGuiAcceptanceNative]::PostMessage($play,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
    Start-Sleep -Milliseconds 400
    [FighterGuiAcceptanceNative]::SendMessage($handle,0x84F1,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
    $before=[FighterGuiAcceptanceNative]::SendMessage($handle,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
    $clock=[Diagnostics.Stopwatch]::StartNew()
    if($Search){Invoke-Control (Control $Window 'libraryClearSearch');Start-Sleep -Milliseconds 700;Type-OwnedEdit (Control $Window 'librarySearch') 'attack';Start-Sleep -Milliseconds 700;Type-OwnedEdit (Control $Window 'librarySearch') 'mario Wait1'}
    Start-Sleep -Milliseconds 3200
    $frames=[FighterGuiAcceptanceNative]::SendMessage($handle,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()-$before
    $seconds=$clock.Elapsed.TotalSeconds;$fps=$frames/$seconds
    [FighterGuiAcceptanceNative]::PostMessage($play,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
    Wait-Until {[FighterGuiAcceptanceNative]::WindowText($play) -eq 'Play'} 'native playback paused'|Out-Null
    Assert ($frames -gt 30) 'Actual target rendering stopped.'
    $maximumGap=[FighterGuiAcceptanceNative]::SendMessage($handle,0x84F2,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()/1000.0
    Assert ($maximumGap -lt 200) 'Metadata browsing stalled native rendering for 200ms or longer.'
    Record 'actual-render-performance' @{Phase=$Phase;Frames=$frames;Seconds=$seconds;FramesPerSecond=$fps;MaximumSwapGapMs=$maximumGap};return $fps
}
$script:app=$null;$fighter=$null;$failure=$null
try{
    if(-not $ResumeAfterRepair){
    $script:app=Launch-App;$main=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'MainForm'}|Select-Object -First 1} 'main window'
    Invoke-FighterMenu $main @('New Fighter');$fighter=Wait-Until {Fighter-Window $app.Id} 'fighter'
    Click-Toolbar $fighter 259;Set-DialogPath $app.Id 'Open Model' $model 'Open'
    Invoke-FighterMenu $fighter @('Animation Library')
    if(-not $ExistingCatalogue){
    Invoke-Control (Control $fighter 'librarySources')
    $settings=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'LibrarySourcesDialog'}|Select-Object -First 1} 'source settings'
    (Find-Control $settings -Name 'Game / Build Paths' -Type $types::TabItem).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Type-OwnedEdit (Control $settings 'libraryLocationBuild') 'Local native acceptance'
    Type-OwnedEdit (Control $settings 'libraryLocationPath') $image;Invoke-Control (Control $settings 'libraryLocationSave')
    Invoke-Control (Control $settings 'libraryBrowseGame')
    Set-DialogPath $app.Id 'Select Installed DolphinTool' $tool 'Open'
    $browser=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'LibraryGameBrowserDialog'}|Select-Object -First 1} 'native disc roster' 120
    Record 'native-roster-shown' @{ScreenshotSha256=(Capture $fighter 'native-roster.png')}
    Invoke-Control (Control $browser 'libraryGameAll')
    (Control $browser 'libraryGameApprove').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Invoke-Control (Control $browser 'libraryGameImport')
    Wait-Until {
        $last=Get-ChildItem -LiteralPath $env:FIGHTERPORT_LIBRARY_ROOT -Filter 'catalogue-*.xml' -ErrorAction SilentlyContinue|Sort-Object Name -Descending|Select-Object -First 1
        $errorDialog=Dialog $app.Id 'Library Sources'
        if($errorDialog){throw 'Native source indexing displayed an error; inspect the captured dialog.'}
        if($last){[xml]$data=Get-Content -LiteralPath $last.FullName;return $data.SelectNodes('/FighterPortAnimationLibrary/Clips/Clip').Count -gt 1000}
    } 'full selected roster catalogue' 900|Out-Null
    Wait-Until {$issues=Dialog $app.Id 'Source inventory issues';if($issues){Invoke-Control (Find-Control $issues -Name 'OK' -Type $types::Button)};$settings.Current.IsEnabled} 'native indexing completes' 120|Out-Null
    # Any unsupported source issues are visible before the final source dialog.
    $issues=Dialog $app.Id 'Source inventory issues';if($issues){Invoke-Control (Find-Control $issues -Name 'OK' -Type $types::Button)}
    Invoke-Control (Control $settings 'librarySourcesClose')
    }
    Wait-Until {(Control $fighter 'libraryPage').Current.Name -match '\d+ total'} 'complete library drawer'
    $last=Get-ChildItem -LiteralPath $env:FIGHTERPORT_LIBRARY_ROOT -Filter 'catalogue-*.xml'|Sort-Object Name -Descending|Select-Object -First 1
    [xml]$catalogue=Get-Content -LiteralPath $last.FullName
    Record 'whole-native-roster-indexed' @{Clips=@($catalogue.FighterPortAnimationLibrary.Clips.Clip).Count;Models=@($catalogue.FighterPortAnimationLibrary.Models.Model).Count;Characters=@($catalogue.FighterPortAnimationLibrary.Models.Model|ForEach-Object Character|Sort-Object -Unique).Count}
    $characterNodes=(Control $fighter 'libraryClips').FindAll($scope::Subtree,[System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::TreeItem))
    $characters=@($catalogue.FighterPortAnimationLibrary.Clips.Clip|ForEach-Object SourceCharacter|Sort-Object -Unique)
    foreach($name in $characters){Assert (@($characterNodes|Where-Object {$_.Current.Name -eq $name}).Count -ge 1) ('Unfiltered browser omitted source character '+$name)}
    Record 'complete-character-outline' @{Characters=$characters.Count;GlobalAnimationPaging=$false}
    Assert (@(Clip-Rows $fighter).Count -eq 0) 'Source indexing changed target assets.'
    Search-Source $fighter;Invoke-Control (Control $fighter 'libraryPreviewOriginal')
    Wait-Until {(Control $fighter 'libraryOriginalLabel').Current.Name.StartsWith('Original: FitMario00')} 'authentic source preview'|Out-Null
    Invoke-Control (Control $fighter 'libraryOriginalPlay');Start-Sleep -Milliseconds 650;Invoke-Control (Control $fighter 'libraryOriginalPlay')
    Record 'disc-original-preview-played' @{ScreenshotSha256=(Capture $fighter 'disc-source-preview.png')}
    Invoke-Control (Control $fighter 'libraryUseOnTarget');Wait-Until {@(Clip-Rows $fighter).Count -eq 1} 'disc source retarget' 1200|Out-Null
    Set-Frame $fighter 1;$first=Capture $fighter 'disc-target-first.png' -Viewport
    Set-Frame $fighter 20;$middle=Capture $fighter 'disc-target-middle.png' -Viewport
    Assert ($first -ne $middle) 'Disc-source target motion did not visibly scrub.'
    $open=Measure-Rendering $fighter 'full-catalogue-search-during-playback' -Search
    Invoke-Control (Control $fighter 'libraryClose');$closed=Measure-Rendering $fighter 'full-catalogue-closed'
    Assert ($open -ge $closed*0.85) 'Full-library search reduced actual rendering more than 15 percent.'
    $native=Join-Path $out 'Disc-Mario-to-Snake.chr0';Click-Toolbar $fighter 607;Set-DialogPath $app.Id 'Export New Native Output' $native 'Save'
    Wait-Until {Test-Path -LiteralPath $native} 'disc-derived native export'|Out-Null
    Invoke-Control (Control $fighter 'btnLoad');Set-DialogPath $app.Id 'Open' $native 'Open';Wait-Until {@(Clip-Rows $fighter).Count -eq 2} 'disc-derived native reopen'|Out-Null
    $project=Join-Path $out 'Disc-Snake.fpstudio';Save-Project $fighter $project
    Record 'disc-retarget-native-roundtrip' @{First=$first;Middle=$middle;OutputSha256=(Get-FileHash $native).Hash}
    Close-App $app $fighter
    # Simulate an offline image only in disposable catalogue metadata.
    $last=Get-ChildItem -LiteralPath $env:FIGHTERPORT_LIBRARY_ROOT -Filter 'catalogue-*.xml'|Sort-Object Name -Descending|Select-Object -First 1
    $xml=[Xml.Linq.XDocument]::Load($last.FullName)
    foreach($mount in $xml.Root.Element('Mounts').Elements()){if($mount.Element('OriginPath')){$mount.Element('OriginPath').Value=Join-Path $out 'offline.wbfs'}}
    foreach($location in $xml.Root.Element('GameLocations').Elements()){if($location.Element('Indexed').Value -eq 'true'){$location.Element('Path').Value=Join-Path $out 'offline.wbfs'}}
    $xml.Save($last.FullName)
    $script:app=Launch-App $project;$fighter=Wait-Until {Fighter-Window $app.Id} 'disc project restart'
    Invoke-FighterMenu $fighter @('Animation Library');Search-Source $fighter 'Missing'
    Invoke-Control (Control $fighter 'libraryRepairSource')
    $repair=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.Name -eq 'Repair Missing Source'}|Select-Object -First 1} 'image repair chooser'
    Invoke-Control (Control $repair 'libraryRepairBrowse');Set-DialogPath $app.Id 'Locate Exact Indexed Source' $image 'Open'
    Wait-Until {$fighter.Current.IsEnabled -and -not (Dialog $app.Id 'Verifying Original Disc')} 'complete exact-image hash verification' 900|Out-Null
    }else{
        $script:app=Launch-App (Join-Path $out 'Disc-Snake.fpstudio');$fighter=Wait-Until {Fighter-Window $app.Id} 'verified disc project restart'
        Wait-Until {@(Clip-Rows $fighter).Count -eq 2} 'independent native target revisions'|Out-Null
        Invoke-FighterMenu $fighter @('Animation Library')
    }
    Search-Source $fighter;Invoke-Control (Control $fighter 'libraryPreviewOriginal')
    Record 'disc-restart-and-image-repair' @{ScreenshotSha256=(Capture $fighter 'disc-repaired.png')}
    Close-App $app $fighter;$receipt.Passed=$true
}catch{$failure=$_;$receipt.Error=$_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace;try{if($fighter){Capture $fighter 'failure.png'|Out-Null}}catch{}}
finally{$receipt.SourceUnchanged=[FighterGuiAcceptanceNative]::HashSourceFile($image) -eq $imageHash -and [FighterGuiAcceptanceNative]::HashSourceFile($model) -eq $modelHash;if(-not $receipt.SourceUnchanged){$receipt.Passed=$false};Write-Receipt}
if($failure){throw $failure};if(-not $receipt.Passed){throw 'Game source GUI failed.'}
