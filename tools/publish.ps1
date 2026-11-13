# Builds a single self-contained BemaniSpiceManager.exe into dist\ (no .NET install needed to run it).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
# If the app is running, its exe is locked: rename it aside (Windows allows that) and publish anyway.
$exe = "$root\dist\BemaniSpiceManager.exe"
Get-ChildItem "$root\dist\*.old.exe" -ErrorAction SilentlyContinue | ForEach-Object { try { Remove-Item $_ -ErrorAction Stop } catch {} }
if (Test-Path $exe) { try { [IO.File]::Open($exe, 'Open', 'ReadWrite', 'None').Close() } catch { Rename-Item $exe "BemaniSpiceManager.$(Get-Date -Format yyyyMMddHHmmss).old.exe" } }
dotnet publish "$root\src\BemaniSpiceManager\BemaniSpiceManager.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o "$root\dist"
Write-Host "Done: $root\dist\BemaniSpiceManager.exe"
