@echo off
setlocal
cd /d "%~dp0"
echo Running Touch Simulator C# command and coordinate-mapping tests...
dotnet run --project "Tests\TouchSimulator.Tests.csproj" -c Release
if errorlevel 1 echo Tests failed. Please send the error shown above.
pause
