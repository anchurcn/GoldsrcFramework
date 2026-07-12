@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "BaseGame=%~1"
if "%BaseGame%"=="" set "BaseGame=hl"

if /I "%BaseGame%"=="hl" (
  call "%~dp0build-hl.bat"
  exit /b %errorlevel%
)

if /I "%BaseGame%"=="bare" (
  if not exist "%~dp0external\bare" (
    echo external\bare is not present. Run:
    echo   git submodule update --init external\bare
    exit /b 1
  )

  call "%~dp0build-bare.bat"
  exit /b %errorlevel%
)

echo Unsupported base game: %BaseGame%
echo Supported values: hl, bare
exit /b 1
