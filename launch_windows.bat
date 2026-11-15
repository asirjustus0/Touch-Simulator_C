@echo off
setlocal
cd /d "%~dp0"
set "LOGDIR=%LOCALAPPDATA%\TouchSimulator\logs"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
if exist "publish\TouchSimulator.exe" (
  echo Launching published TouchSimulator.exe...
  start "" /wait "%CD%\publish\TouchSimulator.exe"
  echo.
  echo Application closed. If it did not display a window, check:
  echo   "%LOGDIR%\startup.log"
  echo   "%CD%\build_log.txt"
  echo.
  echo When the app exits, close this launcher with any key.
  pause
  exit /b 0
)

echo Published EXE not found. Running the source project...
where dotnet >nul 2>&1
if errorlevel 1 (
  echo .NET SDK not found. Install .NET 10 SDK and run build_exe_windows.bat.
  pause
  exit /b 1
)
dotnet run --project "TouchSimulator\TouchSimulator.csproj" -c Debug
set "EXITCODE=%ERRORLEVEL%"
echo.
echo dotnet run exit code: %EXITCODE%
echo Startup errors (if any): "%LOGDIR%\startup.log"
pause
exit /b %EXITCODE%
