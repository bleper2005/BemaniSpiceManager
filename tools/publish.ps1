# Builds a single self-contained BemaniSpiceManager.exe into dist\ (no .NET install needed to run it).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
dotnet publish "$root\src\BemaniSpiceManager\BemaniSpiceManager.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o "$root\dist"
Write-Host "Done: $root\dist\BemaniSpiceManager.exe"
