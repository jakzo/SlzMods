@echo off

taskkill /F /IM BONEWORKS.exe
dotnet build
@REM timeout /T 2 /NOBREAK > NUL
cd /d "C:\Program Files (x86)\Steam\steamapps\common\BONEWORKS\BONEWORKS"
start "" "BONEWORKS.exe"
