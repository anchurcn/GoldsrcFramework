if "%1"=="" (
    echo Error: No source directory provided!
    exit /b 1
)

set "SRC_DIR=%~1"
set "hlmoddir=C:\Program Files (x86)\Steam\steamapps\common\Half-Life\gsfdemo\cl_dlls"


if exist "%SRC_DIR%%" (
    xcopy /E /Y /I /F "%SRC_DIR%" "%hlmoddir%"
)
echo Files copied from: %SRC_DIR%
echo Files copied to: %hlmoddir%
exit /b 0