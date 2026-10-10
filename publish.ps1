# publish.ps1 - Publica una version nueva del Launcher y la deja lista para que
# las copias instaladas la detecten.
#
# Uso (desde la carpeta del proyecto, junto a Launcher.csproj):   .\publish.ps1
# Antes de ejecutarlo, sube <Version> en Launcher.csproj: cada release necesita
# una version distinta de las anteriores.

param(
    [string]$Releases = (Join-Path $env:USERPROFILE "LauncherReleases")
)

$ErrorActionPreference = "Stop"

$csproj = Join-Path $PSScriptRoot "Launcher.csproj"
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version |
    Where-Object { $_ } |
    Select-Object -First 1

if (-not $version) { throw "No se encontro <Version> en Launcher.csproj" }

$publishDir = Join-Path $PSScriptRoot "publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "Publicando Launcher $version ..."
dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo" }

Write-Host "Empaquetando con Velopack en $Releases ..."
vpk pack --packId Launcher --packVersion $version --packDir $publishDir --mainExe Launcher.exe --outputDir $Releases
if ($LASTEXITCODE -ne 0) { throw "vpk pack fallo" }

Write-Host ""
Write-Host "Listo. Version $version publicada en: $Releases"
Write-Host "Primera vez: instala con el Setup.exe de esa carpeta."
Write-Host "Despues, las copias instaladas detectan las versiones nuevas solas."