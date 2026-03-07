dotnet build src/ClassicUO.Client/ClassicUO.Client.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

robocopy bin\Release\net10.0\win-x64 C:\Users\darre\Claude\ClientTest /E /PURGE /NFL /NDL
if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }

Write-Host "Done. Run ClientTest\TazUO.exe"
