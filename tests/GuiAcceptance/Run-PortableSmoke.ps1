[CmdletBinding()]
param([Parameter(Mandatory)][string]$ActualExePath,[Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$Output,[string]$Clip='Run',[int]$Frame=6,[int]$ExpectedClips=0)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GuiTools.ps1')
$exe=(Resolve-Path -LiteralPath $ActualExePath).Path
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
$out=[IO.Path]::GetFullPath($Output)
if((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw 'Use a new or empty output directory.'}
[IO.Directory]::CreateDirectory($out)|Out-Null
$before=(Get-FileHash -LiteralPath $project).Hash
$receipt=[ordered]@{Passed=$false;ExecutableSha256=(Get-FileHash -LiteralPath $exe).Hash;ProjectUnchanged=$false;Steps=[Collections.Generic.List[object]]::new();Error=$null}
function Write-Receipt {$receipt|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $out 'portable-smoke.json') -Encoding UTF8}
function Record([string]$Name,$Data){$receipt.Steps.Add(@{Name=$Name;Data=$Data});Write-Receipt}
$failure=$null;$fighter=$null
try {
    $manifest=Get-Content -LiteralPath (Join-Path (Split-Path -Parent $exe) 'release-manifest.json') -Raw|ConvertFrom-Json
    Assert (-not $manifest.DirtySource -and $manifest.ApplicationSha256 -eq $receipt.ExecutableSha256) 'A final portable package with matching executable is required.'
    $script:app=Launch-App $project
    $fighter=Wait-Until {Fighter-Window $app.Id} 'portable saved fighter restart' 90
    Wait-Until {$live=Fighter-Window $app.Id;$count=@(Clip-Rows $live).Count;$count -gt 0 -and ($ExpectedClips -eq 0 -or $count -eq $ExpectedClips)} 'complete portable animation list' 90 | Out-Null
    $script:controlCache=@{}
    $fighter=Wait-Until {Fighter-Window $app.Id} 'live portable editor'
    Wait-Until {@(Clip-Rows $fighter | Where-Object {$_.Current.Name -eq $Clip}).Count -eq 1} 'portable restored animation' 90 | Out-Null
    Assert-Layout $fighter
    $script:controlCache=@{}
    $fighter=Wait-Until {Fighter-Window $app.Id} 'live portable selection window'
    Select-Clip $fighter $Clip
    Set-Frame $fighter $Frame
    Invoke-FighterMenu $fighter @('Ground Contact View','Low side')
    $fighter=Wait-Until {Fighter-Window $app.Id} 'live portable camera window'
    $image=Capture $fighter 'portable-project-preview.png'
    $feet=Capture $fighter 'portable-project-feet.png' -Viewport
    Assert ((Frame $fighter) -eq $Frame) 'Portable native scrub did not retain the requested frame.'
    Record 'portable-project-visible' @{Clip=(Selected-Clip $fighter);Frame=(Frame $fighter);Screenshot=$image;Feet=$feet;SourceRevision=$manifest.SourceRevision}
    Close-App $app $fighter
    $receipt.Passed=$true
} catch {
    $failure=$_;$receipt.Error=$_.Exception.ToString()+[Environment]::NewLine+$_.ScriptStackTrace
    try{if($fighter){Capture $fighter 'failure.png'|Out-Null}}catch{}
} finally {
    $receipt.ProjectUnchanged=(Get-FileHash -LiteralPath $project).Hash -eq $before
    if(-not $receipt.ProjectUnchanged){$receipt.Passed=$false}
    Write-Receipt
}
if($failure){throw $failure}
if(-not $receipt.ProjectUnchanged){throw 'Existing saved project changed during portable smoke test.'}
