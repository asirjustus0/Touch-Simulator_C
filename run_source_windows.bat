@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "TouchSimulator\TouchSimulator.csproj" -c Debug
set "RESULT=%ERRORLEVEL%"
echo.
echo Source application exited with code: %RESULT%
echo If it closed before showing the UI, inspect:
echo %LOCALAPPDATA%\TouchSimulator\logs\startup.log
pause
exit /b %RESULT%
