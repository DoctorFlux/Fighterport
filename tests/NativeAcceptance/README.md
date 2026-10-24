This local acceptance executable uses the user's Brawl Mario and Snake files. Game files, outputs and machine-specific paths are never committed.

The supported acceptance configuration is Release/x86 on Windows with Visual Studio 2022 MSBuild and the .NET Framework 4.7.2 targeting pack. Run these commands from the repository root after setting the two environment variables to your private local asset folders:

~~~powershell
$env:FIGHTERPORT_TEST_MARIO_ROOT = '<local Mario folder>'
$env:FIGHTERPORT_TEST_SNAKE_ROOT = '<local Snake folder>'

.\scripts\Build.ps1 -Configuration Release
$vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -version '[17.0,18.0)' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild .\tests\NativeAcceptance\NativeAcceptance.csproj /restore /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Acceptance build failed.' }
& .\tests\NativeAcceptance\bin\x86\Release\net472\NativeAcceptance.exe
if ($LASTEXITCODE -ne 0) { throw 'Acceptance run failed.' }
~~~

The application build runs first. BuildProjectReferences=false then builds the harness and copies the existing matching Release application/library, without rebuilding product files used by a concurrent GUI run.

Each source root must contain its FitMario00.pac / FitSnake00.pac costume and matching FitMarioMotionEtc.pac / FitSnakeMotionEtc.pac. Explicit --mario-root and --snake-root arguments can replace the environment variables. Debug/x86 can be used for development by building both projects with Configuration=Debug and using the Debug output directory.

The output directory must be new or empty. Without --output, a unique local temporary directory is created. The report and native outputs stay there. The suite hashes every original file before and after the run.

Checks cover real native model geometry, textures, skeletons, all 297 Mario and 355 Snake CHR0 clips, two sampled fighter motions, native costume and edited CHR0 round trips, model-only projects, separate motion registrations, edited working-copy persistence, reopening in a fresh process, missing or replaced original sources, saved-content integrity, path traversal, version validation, XML DTD rejection and the descriptor size limit. Negative descriptor cases start from valid independent projects and verify the specific rejection reason.

A locked-descriptor case forces an atomic save failure after a real native model edit. It verifies that the old descriptor and committed content identities remain intact, the working edit survives, fresh open still loads the previous revision, and retry after unlocking commits the edit.

A source-independent camera regression replaces a native panel's viewports and verifies that its current camera belongs to the replacement rather than the removed viewport, before any mouse click. It runs on the STA thread without creating a control handle or GL context. Run NativeAcceptance.exe --viewport-only to exercise this check without asset inputs.

These numerical/native checks complement the actual application's GUI acceptance run; they do not claim to verify visible playback on their own.

Playback regressions pump the real Windows Forms dispatcher with a deliberate
stall, checking nonblocking start, UI-thread delivery, a bounded callback queue,
Stop and restart. Real native clips check elapsed subframe sampling, delayed
updates, manual scrubbing, loop duration, last-frame stopping and inherited
integer stepping. No GL handle or synthetic model is needed for these checks.
