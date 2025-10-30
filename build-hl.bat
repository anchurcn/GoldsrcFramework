cd /d "%~dp0"

for /f "usebackq tokens=*" %%i in (`vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do (
  set InstallDir=%%i
)

if exist "%InstallDir%\Common7\Tools\vsdevcmd.bat" (

    "%InstallDir%\Common7\Tools\vsdevcmd.bat" -arch=x86

    MSBuild.exe "external\hlsdk\projects\vs2019\projects.sln" /t:hl_cdll /p:Configuration=Release /p:Platform="Win32" /p:OutDir=..\..\build\
    MSBuild.exe "external\hlsdk\projects\vs2019\projects.sln" /t:hldll /p:Configuration=Release /p:Platform="Win32" /p:OutDir=..\..\build\
)