@echo off
dotnet build src/ClassicUO.Client/ClassicUO.Client.csproj -c Release
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

robocopy bin\Release\net10.0\win-x64 C:\Users\darre\Claude\ClientTest /E /PURGE /NFL /NDL
if %ERRORLEVEL% geq 8 exit /b %ERRORLEVEL%

echo Done. Run ClientTest\TazUO.exe
