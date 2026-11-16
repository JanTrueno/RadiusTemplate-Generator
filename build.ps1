$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

# Self-contained single-file exe (no .NET installation needed on the target PC).
# Trimming removes unused runtime code: ~10.5 MB instead of ~35 MB.
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=true `
    -p:TrimMode=full `
    -o dist

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed (is dist\RadiusMal.exe still running?)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Remove-Item "dist\*.pdb" -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Done: $PSScriptRoot\dist\RadiusMal.exe" -ForegroundColor Green
