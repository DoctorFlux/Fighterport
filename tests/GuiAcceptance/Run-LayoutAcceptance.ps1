[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ActualExePath,
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$Output
)
# Launches the actual app on an idle, unlocked Windows desktop at 100% scaling.
# Requests only window position/size changes. Discards any unsaved test preview state on close.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
$exe = (Resolve-Path -LiteralPath $ActualExePath).Path
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$out = [IO.Path]::GetFullPath($Output)
if ([IO.Path]::GetExtension($exe) -ne '.exe' -or [IO.Path]::GetExtension($project) -ne '.fpstudio') { throw 'Supply the built executable and an existing clean V2 fighter project.' }
if ((Test-Path -LiteralPath $out) -and (-not (Test-Path -LiteralPath $out -PathType Container) -or @(Get-ChildItem -LiteralPath $out -Force).Count)) { throw 'Output must be a new or empty directory.' }
[IO.Directory]::CreateDirectory($out) | Out-Null
$receipt = [ordered]@{Passed=$false;ExecutableSha256=(Get-FileHash -LiteralPath $exe).Hash;ProjectBefore=(Get-FileHash -LiteralPath $project).Hash;ProjectAfter=$null;Sizes=[Collections.Generic.List[object]]::new();Error=$null}
function Write-Receipt { $receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'layout-acceptance.json') -Encoding UTF8 }
function Rect($Bounds) { [ordered]@{Left=$Bounds.Left;Top=$Bounds.Top;Width=$Bounds.Width;Height=$Bounds.Height;Right=$Bounds.Right;Bottom=$Bounds.Bottom} }
function Capture($Window, [string]$Path) {
    Activate-OwnedWindow ([IntPtr]$Window.Current.NativeWindowHandle)
    $bounds = $Window.Current.BoundingRectangle
    $bitmap = [Drawing.Bitmap]::new([int]$bounds.Width,[int]$bounds.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen([int]$bounds.Left,[int]$bounds.Top,0,0,$bitmap.Size); $bitmap.Save($Path,[Drawing.Imaging.ImageFormat]::Png) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
$app = $null
try {
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    Assert ($screen.Width -ge 1336 -and $screen.Height -ge 889) 'Desktop working area must fit the default 1336x889 window.'
    $app = Start-Process -FilePath $exe -ArgumentList ('"' + $project + '"') -WorkingDirectory (Split-Path -LiteralPath $exe) -WindowStyle Normal -PassThru -RedirectStandardOutput (Join-Path $out 'startup.stdout.txt') -RedirectStandardError (Join-Path $out 'startup.stderr.txt')
    $null=$app.Handle
    $fighter = Wait-Until { Fighter-Window $app.Id } 'the actual fighter project window'
    Wait-Until { (Control $fighter 'modelPanel').Current.BoundingRectangle.Width -gt 500 } 'the native viewport' | Out-Null
    $fighter.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
    $transform = $fighter.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
    Assert ($transform.Current.CanResize -and $transform.Current.CanMove) 'The actual fighter window must support native move and resize.'
    $sizes = @(@{Name='default';Width=1336;Height=889},@{Name='minimum';Width=1066;Height=689},@{Name='large';Width=[Math]::Min(1600,$screen.Width);Height=[Math]::Min(1000,$screen.Height)})
    foreach ($size in $sizes) {
        $transform.Resize($size.Width,$size.Height)
        $transform.Move($screen.Left + [Math]::Floor(($screen.Width-$size.Width)/2),$screen.Top + [Math]::Floor(($screen.Height-$size.Height)/2))
        Wait-Until { $b=$fighter.Current.BoundingRectangle; [Math]::Abs($b.Width-$size.Width) -le 2 -and [Math]::Abs($b.Height-$size.Height) -le 2 } "$($size.Name) window size" | Out-Null
        Start-Sleep -Milliseconds 500
        $positions = [ordered]@{Window=(Rect $fighter.Current.BoundingRectangle)}
        foreach ($id in @('fighterToolbar','fighterStatusStrip','menuStrip1','btnPlay','modelPanel')) { $positions[$id] = Rect (Control $fighter $id).Current.BoundingRectangle }
        $receipt.Sizes.Add([ordered]@{Name=$size.Name;RequestedWidth=$size.Width;RequestedHeight=$size.Height;SourceUIPositions=$positions})
        Capture $fighter (Join-Path $out ($size.Name + '.png'))
        Write-Receipt
        Assert ($positions.menuStrip1.Top -ge $positions.fighterToolbar.Bottom) "$($size.Name): the Studio toolbar overlaps the native menu."
        Assert ($positions.btnPlay.Bottom -le $positions.fighterStatusStrip.Top) "$($size.Name): the Studio status bar overlaps native playback controls."
        Assert ($positions.modelPanel.Top -ge $positions.fighterToolbar.Bottom) "$($size.Name): the Studio toolbar overlaps the viewport."
        Assert ($positions.modelPanel.Width -gt 500 -and $positions.modelPanel.Height -gt 300) "$($size.Name): the native viewport is too small."
    }
    $fighter.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Wait-Until {
        $remaining = Fighter-Window $app.Id
        if (-not $remaining) { return $true }
        $discard=Process-Control $app.Id 'No'
        if($discard){Invoke-Control $discard}
        return $false
    } 'the fighter window to close without saving test preview changes' | Out-Null
    Wait-Until {
        foreach ($window in (App-Windows $app.Id)) {
            if ($window.Current.Name -eq 'FighterPort Studio V2') { $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }
        }
        $app.Refresh(); return $app.HasExited
    } 'normal application exit' | Out-Null
    $app.WaitForExit()
    Assert ($app.ExitCode -eq 0) "The actual executable exited with code $($app.ExitCode)."
    $receipt.ProjectAfter = (Get-FileHash -LiteralPath $project).Hash
    Assert ($receipt.ProjectBefore -eq $receipt.ProjectAfter) 'Window resizing changed the saved fighter project.'
    $receipt.Passed = $true
} catch { $receipt.Error = $_.Exception.Message; throw }
finally { $receipt.ProjectAfter = (Get-FileHash -LiteralPath $project).Hash; Write-Receipt }
Write-Output "PASS: actual native layout at three window sizes; evidence: $out"
