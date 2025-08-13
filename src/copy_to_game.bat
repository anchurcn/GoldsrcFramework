@echo off
setlocal enabledelayedexpansion

REM ========================================
REM GoldsrcFramework Demo Post-Build Script
REM ========================================
REM This script copies Demo build output to Half-Life game directory
REM
REM Parameters:
REM   %1 - OutDir (build output directory, e.g.: bin\Debug\net8.0\)
REM
REM Target directories:
REM   - (main game directory)
REM   - (client DLL directory)
REM ========================================

REM Check parameters
if "%~1"=="" (
    echo Error: OutDir parameter not provided
    echo Usage: %0 ^<OutDir^>
    echo Example: %0 "bin\Debug\net8.0\"
    exit /b 1
)

REM Set variables
set "SOURCE_DIR=%~1"
set "GAME_ROOT=<path-to-hl.exe>"
set "MOD_DIR=%GAME_ROOT%\gsfdemo"
set "CL_DLLS_DIR=%MOD_DIR%\cl_dlls"

REM Ensure source directory exists
if not exist "%SOURCE_DIR%" (
    echo Error: Source directory does not exist: %SOURCE_DIR%
    exit /b 1
)

REM Ensure target directory exists
if not exist "%GAME_ROOT%" (
    echo Error: Game root directory does not exist: %GAME_ROOT%
    exit /b 1
)

REM Create mod directory (if not exists)
if not exist "%MOD_DIR%" (
    echo Creating mod directory: %MOD_DIR%
    mkdir "%MOD_DIR%"
)

REM Create cl_dlls directory (if not exists)
if not exist "%CL_DLLS_DIR%" (
    echo Creating cl_dlls directory: %CL_DLLS_DIR%
    mkdir "%CL_DLLS_DIR%"
)

echo ========================================
echo Starting file copy...
echo Source directory: %SOURCE_DIR%
echo ========================================

REM Copy all files to main game directory
echo.
echo [1/2] Copying all files to main game directory...
echo Target: %GAME_ROOT%
xcopy "%SOURCE_DIR%*" "%GAME_ROOT%\" /Y /E /I
if !errorlevel! neq 0 (
    echo Warning: Error occurred while copying to main game directory
)

REM Copy client DLLs to cl_dlls directory
echo.
echo [2/2] Copying client DLLs to cl_dlls directory...
echo Target: %CL_DLLS_DIR%

REM Copy client.dll (if exists)
if exist "%SOURCE_DIR%client.dll" (
    echo Copying: client.dll
    copy "%SOURCE_DIR%client.dll" "%CL_DLLS_DIR%\" /Y
    if !errorlevel! neq 0 (
        echo Warning: Error occurred while copying client.dll
    )
) else (
    echo Note: client.dll does not exist in source directory
)

REM Copy loader.dll (if exists)
if exist "%SOURCE_DIR%loader.dll" (
    echo Copying: loader.dll
    copy "%SOURCE_DIR%loader.dll" "%CL_DLLS_DIR%\" /Y
    if !errorlevel! neq 0 (
        echo Warning: Error occurred while copying loader.dll
    )
) else (
    echo Note: loader.dll does not exist in source directory
)

REM Copy other possible client-related files
for %%f in (gsfloader.dll nethost.dll) do (
    if exist "%SOURCE_DIR%%%f" (
        echo Copying: %%f
        copy "%SOURCE_DIR%%%f" "%CL_DLLS_DIR%\" /Y
        if !errorlevel! neq 0 (
            echo Warning: Error occurred while copying %%f
        )
    )
)

echo.
echo ========================================
echo Copy completed!
echo ========================================
echo Files have been copied to:
echo   - %GAME_ROOT%
echo   - %CL_DLLS_DIR%
echo ========================================

endlocal
exit /b 0
