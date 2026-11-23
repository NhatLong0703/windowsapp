@echo off
cd /d "%~dp0"
rem Tat ban dang chay (neu co) de khong khoa file
taskkill /f /im WebApplication1.exe >nul 2>&1
rem Xoa ban build cu de Views moi luon duoc bien dich lai
rmdir /s /q App 2>nul
rmdir /s /q obj 2>nul
rmdir /s /q bin 2>nul
dotnet publish WebApplication1.csproj -c Release -r win-x64 --self-contained true -o "%~dp0App"
echo.
echo Xong! Mo thu muc App de chay WebApplication1.exe
pause
