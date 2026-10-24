# Run with Windows PowerShell 5.1. Requires VS2022's .NET Framework SDK tools.
if (-not ('FighterNativeElement' -as [type])) {
    $nativeCache=Join-Path ([IO.Path]::GetTempPath()) 'FighterPort-UIA'
    [IO.Directory]::CreateDirectory($nativeCache) | Out-Null
    $interop=Join-Path $nativeCache 'Interop.UIAutomationClient.dll'
    if (-not (Test-Path -LiteralPath $interop)) {
        $sdk=Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\TlbImp.exe'
        if (-not (Test-Path -LiteralPath $sdk)) { throw 'Windows SDK TlbImp.exe is required for native desktop acceptance.' }
        & $sdk (Join-Path $env:WINDIR 'System32\UIAutomationCore.dll') "/out:$interop" /namespace:NativeUIA /silent
        if ($LASTEXITCODE -ne 0) { throw 'Windows UI Automation interop generation failed.' }
    }
    Add-Type -Path $interop
    Add-Type -Path (Join-Path $PSScriptRoot 'NativeUia.cs') -ReferencedAssemblies $interop,UIAutomationClient,UIAutomationTypes,WindowsBase,System.Core
}
$uia=[FighterNativeElement]
