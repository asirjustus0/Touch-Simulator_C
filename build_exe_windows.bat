@echo off
setlocal
cd /d "%~dp0"
echo Building Touch Simulator (C# / .NET 10)...
where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: .NET 10 SDK not found. Download from https://dotnet.microsoft.com/download/dotnet/10.0
  pause
  exit /b 1
)
dotnet --version
echo Build log: "%CD%\build_log.txt"
dotnet publish "TouchSimulator\TouchSimulator.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish" > "build_log.txt" 2>&1
set "RESULT=%ERRORLEVEL%"
type "build_log.txt"
if not "%RESULT%"=="0" (
  echo.
  echo BUILD FAILED - send build_log.txt for diagnosis.
  pause
  exit /b %RESULT%
)
echo.
echo BUILD SUCCEEDED: %CD%\publish\TouchSimulator.exe
echo Use launch_windows.bat to start and retain diagnostic information.
pause
