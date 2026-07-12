@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "VsWhere=vswhere"
set "VsWhereFallback=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
where vswhere > nul 2> nul
if errorlevel 1 (
  set "VsWhere=!VsWhereFallback!"
)

if not exist "!VsWhere!" (
  echo vswhere.exe was not found.
  echo Expected it in PATH or at:
  echo   !VsWhereFallback!
  exit /b 1
)

for /f "usebackq tokens=*" %%i in (`"!VsWhere!" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do (
  set InstallDir=%%i
)

if not exist "%InstallDir%\Common7\Tools\vsdevcmd.bat" (
  echo Visual Studio dev command prompt not found.
  exit /b 1
)

call "%InstallDir%\Common7\Tools\vsdevcmd.bat" -arch=x86

if "%LegacyPlatformToolset%"=="" (
  set "LegacyPlatformToolset=v145"
  if "%VisualStudioVersion%"=="17.0" set "LegacyPlatformToolset=v143"
  if "%VisualStudioVersion%"=="16.0" set "LegacyPlatformToolset=v142"
)

echo Using PlatformToolset=%LegacyPlatformToolset%

set "SolutionDir=%~dp0external\bare\projects\vs2019\"
set "ClientOutDir=%SolutionDir%Release\hl_cdll"
set "ServerOutDir=%SolutionDir%Release\hldll"
set "LegacyDir=%~dp0artifacts\legacy\win-x86\bare"

MSBuild.exe "%SolutionDir%projects.sln" /t:hl_cdll /p:Configuration=Release /p:Platform="Win32" /p:PlatformToolset=%LegacyPlatformToolset% /p:PostBuildEventUseInBuild=false
set "BuildExitCode=!errorlevel!"
if not "!BuildExitCode!"=="0" exit /b !BuildExitCode!

MSBuild.exe "%SolutionDir%projects.sln" /t:hldll /p:Configuration=Release /p:Platform="Win32" /p:PlatformToolset=%LegacyPlatformToolset% /p:PostBuildEventUseInBuild=false
set "BuildExitCode=!errorlevel!"
if not "!BuildExitCode!"=="0" exit /b !BuildExitCode!

if not exist "%LegacyDir%" mkdir "%LegacyDir%"

copy /Y "%ClientOutDir%\client.dll" "%LegacyDir%\libclient.dll" > nul
if errorlevel 1 (
  echo Failed to copy client.dll from %ClientOutDir%
  exit /b 1
)

copy /Y "%ClientOutDir%\client.pdb" "%LegacyDir%\libclient.pdb" > nul

copy /Y "%ServerOutDir%\hl.dll" "%LegacyDir%\libserver.dll" > nul
if errorlevel 1 (
  echo Failed to copy hl.dll from %ServerOutDir%
  exit /b 1
)

copy /Y "%ServerOutDir%\hl.pdb" "%LegacyDir%\libserver.pdb" > nul

echo Legacy bare DLLs built and copied to %LegacyDir%
echo   client.dll -> libclient.dll
echo   hl.dll     -> libserver.dll
