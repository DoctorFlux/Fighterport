[CmdletBinding()]
param([Parameter(Mandatory)][string]$ActualExePath,[Parameter(Mandatory)][string]$MarioRoot,
    [Parameter(Mandatory)][string]$SnakeRoot,[Parameter(Mandatory)][string]$Output,
    [string]$GameVersion='Build not supplied',[string]$LibraryRoot,[string]$GamePath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
$exe=(Resolve-Path -LiteralPath $ActualExePath).Path
$out=[IO.Path]::GetFullPath($Output)
if((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw 'Choose an empty output directory.'}
[IO.Directory]::CreateDirectory($out)|Out-Null
if($LibraryRoot){$env:FIGHTERPORT_LIBRARY_ROOT=[IO.Path]::GetFullPath($LibraryRoot)}
$mario=(Resolve-Path -LiteralPath $MarioRoot).Path;$snake=(Resolve-Path -LiteralPath $SnakeRoot).Path
$before=@{};foreach($file in Get-ChildItem -LiteralPath $mario,$snake -File -Recurse){$before[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash}
$receipt=[ordered]@{Passed=$false;ExecutableSha256=(Get-FileHash -LiteralPath $exe).Hash;Steps=[Collections.Generic.List[object]]::new();SourcesUnchanged=$false;Error=$null}
function Write-Receipt {$receipt|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $out 'library-gui.json') -Encoding UTF8}
function Record([string]$Name,$Data){$receipt.Steps.Add(@{Name=$Name;Data=$Data});Write-Receipt}
function Open-Library($Window){Invoke-FighterMenu $Window @('Animation Library');Wait-Until {(Control $Window 'librarySearch').Current.IsOffscreen -eq $false} 'library drawer'|Out-Null}
function Library-Row($Window,[string]$Character){
    $list=Control $Window 'libraryClips'
    $items=$list.FindAll($scope::Subtree,[System.Windows.Automation.Condition]::TrueCondition)
    foreach($item in $items){try{$name=[string]$item.Current.Name;if($name.StartsWith('Wait1 · Fit'+$Character+'00 · Available')){return $item}}catch{}}
}
function Search-Wait($Window,[string]$Character){
    Type-OwnedEdit (Control $Window 'librarySearch') ($Character+' Wait1')
    Wait-Until {try{$row=Library-Row $Window $Character;if($row){$row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();return (Control $Window 'libraryPreviewOriginal').Current.IsEnabled}}catch{}} 'verified library Wait1'|Out-Null
    Wait-Until {(Control $Window 'libraryPreviewOriginal').Current.IsEnabled} 'available source actions'|Out-Null
}
function Assert-CompleteCharacter($Window,[string]$Character){
    Invoke-Control (Control $Window 'libraryClearSearch')
    $index=Get-ChildItem -LiteralPath $env:FIGHTERPORT_LIBRARY_ROOT -Filter 'catalogue-*.xml'|Sort-Object Name|Select-Object -Last 1
    [xml]$data=Get-Content -LiteralPath $index.FullName
    $sourceModel=@($data.FighterPortAnimationLibrary.Models.Model|Where-Object {$_.Character -eq $Character -and $_.Name -eq ('Fit'+$Character+'00')})[0]
    $expected=@($data.FighterPortAnimationLibrary.Clips.Clip|Where-Object {$_.SelectSingleNode('ModelId') -and $_.SelectSingleNode('ModelId').InnerText -eq $sourceModel.Id}).Count
    $total=@($data.FighterPortAnimationLibrary.Clips.Clip).Count
    Wait-Until {(Control $Window 'libraryPage').Current.Name -eq ($total.ToString()+' matches '+[char]0xB7+' '+$total+' total')} 'unfiltered complete catalogue'|Out-Null
    $characterNode=Find-Control (Control $Window 'libraryClips') -Name $Character -Type $types::TreeItem
    $characterNode.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $modelNode=Wait-Until {Find-Control $characterNode -Name ('Fit'+$Character+'00') -Type $types::TreeItem} 'incrementally loaded source model'
    $modelNode.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Wait-Until {$listed=$modelNode.FindAll($scope::Descendants,[System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::TreeItem));$listed.Count -eq $expected -and -not @($listed|Where-Object {$_.Current.Name -like 'Loading animations*'}).Count} 'all character animation entries'|Out-Null
    $rows=$modelNode.FindAll($scope::Descendants,[System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::TreeItem))
    Assert ($expected -gt 250 -and $rows.Count -eq $expected) 'Expanded original model omitted indexed character animations.'
    Record 'complete-unfiltered-character-animations' @{Character=$Character;Expected=$expected;Listed=$rows.Count;ScreenshotSha256=(Capture $Window ($Character+'-all-library-animations.png'))}
}
function Add-Source($Window,[string]$Character,[string]$Root){
    $beforeGeneration=@(Get-ChildItem -LiteralPath $env:FIGHTERPORT_LIBRARY_ROOT -Filter 'catalogue-*.xml' -ErrorAction SilentlyContinue|Sort-Object Name|Select-Object -Last 1 -ExpandProperty Name)
    Invoke-Control (Control $Window 'libraryAddSource')
    $dialog=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'LibrarySourceDialog'}|Select-Object -First 1} 'source registration dialog'
    Invoke-Control (Control $dialog 'libraryModelFileBrowse');Set-DialogPath $app.Id 'Choose Original Source Model' (Join-Path $Root ('Fit'+$Character+'00.pac')) 'Open'
    $combo=Control $dialog 'librarySourceModel'
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $choice=Wait-Until {
        foreach($window in App-Windows $app.Id){$items=$window.FindAll($scope::Subtree,[System.Windows.Automation.PropertyCondition]::new($uia::ControlTypeProperty,$types::ListItem));foreach($item in $items){if($item.Current.Name.StartsWith('Fit'+$Character+'00  [') -and -not $item.Current.IsOffscreen){return $item}}}
    } 'exact original model choice'
    $choice.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    try{$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()}catch{}
    Type-OwnedEdit (Control $dialog 'libraryCharacter') $Character
    Invoke-Control (Control $dialog 'libraryMotionFileBrowse');Set-DialogPath $app.Id 'Choose Original Motion Set' (Join-Path $Root ('Fit'+$Character+'MotionEtc.pac')) 'Open'
    Type-OwnedEdit (Control $dialog 'libraryGameVersion') $GameVersion
    $approval=Control $dialog 'libraryApproveAssociation'
    $approval.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Invoke-Control (Control $dialog 'libraryRegisterSource')
    Wait-Until {-not @(App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'LibrarySourceDialog'}).Count} 'source review closes'|Out-Null
    Wait-Until {
        $current=@(Get-ChildItem -LiteralPath $env:FIGHTERPORT_LIBRARY_ROOT -Filter 'catalogue-*.xml' -ErrorAction SilentlyContinue|Sort-Object Name|Select-Object -Last 1 -ExpandProperty Name)
        $current.Count -gt 0 -and ($beforeGeneration.Count -eq 0 -or $current[0] -ne $beforeGeneration[0])
    } 'whole source catalogue publication'|Out-Null
    Wait-Until {$Window.Current.IsEnabled -and (Control $Window 'libraryPage').Current.Name -match '\d+ total'} 'source indexing finishes before typing search'|Out-Null
    Search-Wait $Window $Character
    Record 'whole-source-set-registered' @{Source=$Character;Build=$GameVersion;Page=(Control $Window 'libraryPage').Current.Name}
}
function Source-Capture($Window,[string]$Name){
    Activate-OwnedWindow ([IntPtr]$Window.Current.NativeWindowHandle)
    $bounds=(Control $Window 'libraryOriginalViewport').Current.BoundingRectangle
    $bitmap=[Drawing.Bitmap]::new([int]$bounds.Width,[int]$bounds.Height);$graphics=[Drawing.Graphics]::FromImage($bitmap)
    $path=Join-Path $out $Name
    try{
        $graphics.CopyFromScreen([int]$bounds.X,[int]$bounds.Y,0,0,$bitmap.Size)
        $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
        $pixels=0;for($y=8;$y -lt $bitmap.Height-8;$y+=2){for($x=8;$x -lt $bitmap.Width-8;$x+=2){$c=$bitmap.GetPixel($x,$y);if([Math]::Max($c.R,[Math]::Max($c.G,$c.B)) -lt 170){$pixels++}}}
        Assert ($pixels -gt 100) 'Original source model is not visibly rendered.'
    }finally{$graphics.Dispose();$bitmap.Dispose()}
    return (Get-FileHash -LiteralPath $path).Hash
}
function Measure-TargetPlayback($Window,[string]$Phase){
    Pause-Playback $Window;Set-Frame $Window 1
    $toggle=(Control $Window 'chkLoop').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On){$toggle.Toggle()}
    $viewport=Control $Window 'modelPanel'
    $handle=[IntPtr]$viewport.Current.NativeWindowHandle
    if($handle -eq [IntPtr]::Zero){throw 'No owned target viewport HWND for actual-render metrics.'}
    [FighterGuiAcceptanceNative]::SendMessage($handle,0x84F1,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
    $before=[FighterGuiAcceptanceNative]::SendMessage($handle,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()
    $clock=[Diagnostics.Stopwatch]::StartNew();$cpu=$app.TotalProcessorTime.TotalMilliseconds
    Invoke-Control (Control $Window 'btnPlay');Start-Sleep -Milliseconds 3200
    $rendered=[FighterGuiAcceptanceNative]::SendMessage($handle,0x84F0,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()-$before
    $seconds=$clock.Elapsed.TotalSeconds;$app.Refresh();$cpu=($app.TotalProcessorTime.TotalMilliseconds-$cpu)/$seconds
    Pause-Playback $Window
    $fps=$rendered/$seconds
    Assert ($rendered -gt 30) 'Real target renderer did not keep advancing during the performance sample.'
    $maximumGap=[FighterGuiAcceptanceNative]::SendMessage($handle,0x84F2,[IntPtr]::Zero,[IntPtr]::Zero).ToInt32()/1000.0
    Record 'actual-target-render-performance' @{Phase=$Phase;RenderedFrames=$rendered;Seconds=$seconds;FramesPerSecond=$fps;CpuMillisecondsPerSecond=$cpu;MaximumSwapGapMs=$maximumGap}
    return $fps
}
$fighter=$null;$script:app=$null;$failure=$null
try{
    $directions=@(@{Source='Mario';Target='Snake';SourceRoot=$mario;TargetRoot=$snake},@{Source='Snake';Target='Mario';SourceRoot=$snake;TargetRoot=$mario})
    foreach($direction in $directions){
        $script:app=Launch-App
        $main=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'MainForm'}|Select-Object -First 1} 'main window'
        Invoke-FighterMenu $main @('New Fighter');$fighter=Wait-Until {Fighter-Window $app.Id} 'new library fighter'
        Click-Toolbar $fighter 259;Set-DialogPath $app.Id 'Open Model' (Join-Path $direction.TargetRoot ('Fit'+$direction.Target+'00.pac')) 'Open'
        Open-Library $fighter;Assert-Layout $fighter
        Add-Source $fighter $direction.Source $direction.SourceRoot
        Assert-CompleteCharacter $fighter $direction.Source
        $category=Find-Control (Control $fighter 'libraryClips') -Name 'Brawl' -Type $types::TreeItem
        $category.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
        Assert ($category.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -eq 0) 'Game category did not collapse.'
        $category.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Search-Wait $fighter $direction.Source
        $characterGroup=Find-Control (Control $fighter 'libraryClips') -Name $direction.Source -Type $types::TreeItem
        $characterGroup.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
        Assert ($characterGroup.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -eq 0) 'Character group did not collapse.'
        $characterGroup.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        $modelGroup=Find-Control (Control $fighter 'libraryClips') -Name ('Fit'+$direction.Source+'00') -Type $types::TreeItem
        $modelGroup.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
        Assert ($modelGroup.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -eq 0) 'Source model group did not collapse.'
        $modelGroup.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Search-Wait $fighter $direction.Source
        Record 'game-character-model-collapse-expand' @{Game='Brawl';Character=$direction.Source;OriginalModelName=('Fit'+$direction.Source+'00')}
        if($GamePath -and $direction.Source -eq 'Mario'){
            Invoke-Control (Control $fighter 'librarySources')
            $settings=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'LibrarySourcesDialog'}|Select-Object -First 1} 'source settings'
            (Find-Control $settings -Name 'Game / Build Paths' -Type $types::TabItem).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Type-OwnedEdit (Control $settings 'libraryLocationBuild') $GameVersion
            Type-OwnedEdit (Control $settings 'libraryLocationPath') $GamePath
            Invoke-Control (Control $settings 'libraryLocationSave')
            Invoke-Control (Control $settings 'librarySourcesClose')
            Record 'configured-game-build-path' @{Game='Brawl';Build=$GameVersion;ConfigurationOnly=$true}
        }
        Assert (@(Clip-Rows $fighter).Count -eq 0) 'Library registration altered target animation assets.'
        $initial=Capture $fighter ($direction.Target+'-target-before.png') -Viewport
        Invoke-Control (Control $fighter 'libraryPreviewOriginal')
        Wait-Until {(Control $fighter 'libraryOriginalLabel').Current.Name -match ('Original: Fit'+$direction.Source+'00')} 'original model identity'|Out-Null
        Start-Sleep -Milliseconds 800
        $sourceFirst=Source-Capture $fighter ($direction.Source+'-original-first.png')
        Invoke-Control (Control $fighter 'libraryOriginalPlay');Start-Sleep -Milliseconds 650;Invoke-Control (Control $fighter 'libraryOriginalPlay')
        $sourceMiddle=Source-Capture $fighter ($direction.Source+'-original-played.png')
        Assert ($sourceFirst -ne $sourceMiddle) 'Original source playback did not change rendered pixels.'
        Assert ($initial -eq (Capture $fighter ($direction.Target+'-target-after-source-preview.png') -Viewport)) 'Original-source preview replaced or posed the editable target.'
        Record 'original-source-playback' @{Source=$direction.Source;Target=$direction.Target;First=$sourceFirst;Played=$sourceMiddle;TargetPreserved=$true}
        Invoke-Control (Control $fighter 'libraryUseOnTarget')
        Wait-Until {@(Clip-Rows $fighter).Count -eq 1} 'automatic source-bound retarget' 1200|Out-Null
        Assert (@(App-Windows $app.Id|Where-Object {$_.Current.Name -eq 'Choose Animation Source Model'}).Count -eq 0) 'Verified reuse asked for the source model again.'
        Set-Frame $fighter 1;$first=Capture $fighter ($direction.Target+'-retarget-first.png') -Viewport
        Set-Frame $fighter 20;$middle=Capture $fighter ($direction.Target+'-retarget-middle.png') -Viewport
        Assert ($first -ne $middle) 'Retargeted source-bound clip did not visibly scrub.'
        Set-Frame $fighter 1;Invoke-Control (Control $fighter 'btnPlay');Start-Sleep -Milliseconds 700;Pause-Playback $fighter
        Assert ((Frame $fighter) -gt 1) 'Target native playback did not advance.'
        $openFps=Measure-TargetPlayback $fighter 'library-open-source-paused'
        Invoke-Control (Control $fighter 'libraryClose')
        $closedFps=Measure-TargetPlayback $fighter 'library-closed'
        Open-Library $fighter
        Assert ($openFps -ge $closedFps*0.85) 'Library drawer reduces measured target rendering by more than 15 percent.'
        $native=Join-Path $out ($direction.Target+'-Wait1.chr0');Click-Toolbar $fighter 607;Set-DialogPath $app.Id 'Export New Native Output' $native 'Save'
        Wait-Until {Test-Path -LiteralPath $native} 'library native export'|Out-Null
        Invoke-Control (Control $fighter 'btnLoad');Set-DialogPath $app.Id 'Open' $native 'Open'
        Wait-Until {@(Clip-Rows $fighter).Count -eq 2} 'fresh library output native reopen'|Out-Null
        $project=Join-Path $out ($direction.Target+'-Library.fpstudio');Save-Project $fighter $project
        Record 'source-bound-retarget-native-export' @{Source=$direction.Source;Target=$direction.Target;NativeSha256=(Get-FileHash $native).Hash;First=$first;Middle=$middle}
        Close-App $app $fighter;$script:app=Launch-App $project;$fighter=Wait-Until {Fighter-Window $app.Id} 'source-bound project restart'
        Wait-Until {@(Clip-Rows $fighter).Count -eq 2} 'restored target revisions'|Out-Null
        Open-Library $fighter;Search-Wait $fighter $direction.Source
        Record 'shared-library-and-project-restart' @{Target=$direction.Target;ScreenshotSha256=(Capture $fighter ($direction.Target+'-restart.png'))}
        Close-App $app $fighter
    }
    # Exercise repair on disposable copies, leaving the user's original mounts
    # and normal shared library untouched.
    $repairRoot=Join-Path $out 'RepairFixtures';[IO.Directory]::CreateDirectory($repairRoot)|Out-Null
    foreach($name in @('FitMario00.pac','FitMarioMotionEtc.pac')){Copy-Item -LiteralPath (Join-Path $mario $name) -Destination (Join-Path $repairRoot $name)}
    $env:FIGHTERPORT_LIBRARY_ROOT=Join-Path $out 'RepairLibrary'
    $script:app=Launch-App;$main=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'MainForm'}|Select-Object -First 1} 'repair main window'
    Invoke-FighterMenu $main @('New Fighter');$fighter=Wait-Until {Fighter-Window $app.Id} 'repair fighter'
    Open-Library $fighter;Add-Source $fighter 'Mario' $repairRoot
    Invoke-Control (Control $fighter 'libraryUndo')
    Wait-Until {(Control $fighter 'libraryPage').Current.Name -eq 'No clips'} 'first registration Undo is atomic'|Out-Null
    Add-Source $fighter 'Mario' $repairRoot
    $oldMotion=Join-Path $repairRoot 'FitMarioMotionEtc.pac';$newMotion=Join-Path $repairRoot 'relocated-motion.pac'
    Move-Item -LiteralPath $oldMotion -Destination $newMotion
    Type-OwnedEdit (Control $fighter 'librarySearch') 'Mario Wait1 '
    $missing=Wait-Until {
        foreach($row in (Control $fighter 'libraryClips').FindAll($scope::Subtree,[System.Windows.Automation.Condition]::TrueCondition)){
            if(([string]$row.Current.Name).StartsWith('Wait1 · FitMario00 · Missing')){return $row}
        }
    } 'missing source visible'
    $missing.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Invoke-Control (Control $fighter 'libraryRepairSource')
    $repair=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.Name -eq 'Repair Missing Source'}|Select-Object -First 1} 'repair source chooser'
    Invoke-Control (Control $repair 'libraryRepairBrowse');Set-DialogPath $app.Id 'Locate Exact Indexed Source' $newMotion 'Open'
    Search-Wait $fighter 'Mario'
    Invoke-Control (Control $fighter 'libraryPreviewOriginal')
    Wait-Until {(Control $fighter 'libraryOriginalLabel').Current.Name.StartsWith('Original: FitMario00')} 'repaired source renders'|Out-Null
    Record 'repair-exact-relocated-source' @{OriginalsPreserved=$true;ScreenshotSha256=(Capture $fighter 'repaired-source.png')}
    Invoke-Control (Control $fighter 'librarySources')
    $settings=Wait-Until {App-Windows $app.Id|Where-Object {$_.Current.AutomationId -eq 'LibrarySourcesDialog'}|Select-Object -First 1} 'registered sources dialog'
    $sourceItem=Find-Control (Control $settings 'libraryRegisteredSources') -Type $types::ListItem
    $sourceItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Invoke-Control (Control $settings 'libraryRemoveSource');Invoke-Control (Control $settings 'librarySourcesClose')
    Wait-Until {(Control $fighter 'libraryPage').Current.Name -eq 'No clips'} 'removed source clips disappear'|Out-Null
    Assert ((Test-Path -LiteralPath (Join-Path $repairRoot 'FitMario00.pac')) -and (Test-Path -LiteralPath $newMotion)) 'Remove Source deleted fixture files.'
    Invoke-Control (Control $fighter 'libraryUndo');Search-Wait $fighter 'Mario'
    Record 'remove-source-and-undo' @{OriginalFilesPreserved=$true;Restored=$true}
    Close-App $app $fighter
    $receipt.Passed=$true
}catch{
    $failure=$_;$receipt.Error=$_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace
    try{if($fighter){Capture $fighter 'failure.png'|Out-Null}}catch{}
    try{if($app -and -not $app.HasExited){$live=Fighter-Window $app.Id;if($live){Pause-Playback $live;Close-App $app $live}}}catch{$receipt['CleanupError']=$_.Exception.Message}
}finally{
    $after=@{};foreach($file in Get-ChildItem -LiteralPath $mario,$snake -File -Recurse){$after[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash}
    $receipt.SourcesUnchanged=$after.Count -eq $before.Count -and @($before.Keys|Where-Object {-not $after.ContainsKey($_) -or $before[$_] -ne $after[$_]}).Count -eq 0
    if(-not $receipt.SourcesUnchanged){$receipt.Passed=$false};Write-Receipt
}
if($failure){throw $failure};if(-not $receipt.SourcesUnchanged){throw 'Original hashes changed.'}
