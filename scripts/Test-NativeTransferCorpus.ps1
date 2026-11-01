[CmdletBinding()]
param([Parameter(Mandatory)][string]$NativeDirectory,[Parameter(Mandatory)][string]$Manifest,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
[Diagnostics.Process]::GetCurrentProcess().PriorityClass='BelowNormal'
$native=(Resolve-Path -LiteralPath $NativeDirectory).Path
$inputFile=(Resolve-Path -LiteralPath $Manifest).Path
if((Get-Item -LiteralPath $inputFile).Length -gt 1048576){throw 'Unbounded corpus manifest.'}
$plan=Get-Content -LiteralPath $inputFile -Raw | ConvertFrom-Json
if($plan.Schema -ne 'fighterport-native-transfer-corpus-1' -or $plan.Cases.Count -lt 1 -or $plan.Cases.Count -gt 128){throw 'Invalid bounded corpus.'}
if(@($plan.Cases.Id | Select-Object -Unique).Count -ne $plan.Cases.Count -or @($plan.Cases | Where-Object {$_.Id -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,95}$'}).Count){throw 'Case identities must be unique safe directory names.'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use fresh evidence; previous results are retained.'}
New-Item -ItemType Directory -Path $output | Out-Null
$runner=Join-Path $native 'RetargetAcceptance.exe'
$rows=@();$complete=$false
function Save-CorpusReceipt {
    [ordered]@{Schema='fighterport-native-transfer-results-1';Complete=$complete;ManifestSha256=(Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash;AppSha256=(Get-FileHash -LiteralPath (Join-Path $native 'FighterPortStudioV2.exe') -Algorithm SHA256).Hash;Cases=$rows} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
}
Save-CorpusReceipt
foreach($case in $plan.Cases){
    while(Test-Path -LiteralPath (Join-Path $output 'pause')){if(Test-Path -LiteralPath (Join-Path $output 'cancel')){break};Start-Sleep -Milliseconds 200}
    if(Test-Path -LiteralPath (Join-Path $output 'cancel')){break}
    $folder=Join-Path $output $case.Id
    & $runner --retarget-case $case.SourceModel $case.TargetModel $case.Motion $case.Fixture $folder *> (Join-Path $output ($case.Id+'.log'))
    $exitCode=$LASTEXITCODE
    $row=[ordered]@{Id=$case.Id;PreparationExit=$exitCode;MasterReady=$false;DefaultReady=$false;DenseOracleExit=$null;DefaultOracleExit=$null}
    $receipt=Join-Path $folder 'case.json'
    if(Test-Path -LiteralPath $receipt){
        $details=Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
        $row.MasterReady=$details.MasterReady;$row.DefaultReady=$details.DefaultReady
        if($details.MasterReady){
            & $runner --verify-trajectory $case.SourceModel $case.TargetModel $case.Motion $case.Clip (Join-Path $folder 'dense-target.chr0') (Join-Path $folder 'dense-oracle') *> (Join-Path $output ($case.Id+'-dense-oracle.log'))
            $row.DenseOracleExit=$LASTEXITCODE
        }
        if($details.DefaultReady){
            & $runner --verify-trajectory $case.SourceModel $case.TargetModel $case.Motion $case.Clip (Join-Path $folder 'current-target.chr0') (Join-Path $folder 'default-oracle') *> (Join-Path $output ($case.Id+'-default-oracle.log'))
            $row.DefaultOracleExit=$LASTEXITCODE
        }
    }
    $rows+=[pscustomobject]$row;Save-CorpusReceipt
    Write-Output ($case.Id+': preparation='+$exitCode+'; master oracle='+$row.DenseOracleExit+'; default oracle='+$row.DefaultOracleExit)
}
$complete=$rows.Count -eq $plan.Cases.Count
Save-CorpusReceipt
