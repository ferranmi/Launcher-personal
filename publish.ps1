# publish.ps1 - Publica una version nueva del Launcher y la deja lista para que
# las copias instaladas la detecten.
#
# Uso (desde la carpeta del proyecto, junto a Launcher.csproj):   .\publish.ps1
# Antes de ejecutarlo, sube <Version> en Launcher.csproj: cada release necesita
# una version distinta de las anteriores.
#
# Nota: la carpeta "publish" es solo un paso intermedio. No abras nada de ella:
# la app se usa instalada con el Setup.exe de la carpeta de versiones.
# (Solo ASCII en los textos: Windows PowerShell 5.1 lee mal los acentos de un
# .ps1 guardado como UTF-8 sin BOM.)

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

# 1) Cierra cualquier proceso que se este ejecutando desde la carpeta publish (p.ej. una
#    copia abierta a mano, o el Launcher que sigue vivo en la bandeja). Era la causa de
#    que la carpeta apareciera "bloqueada". No toca la copia instalada.
$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"
$prefix = $publishDir.TrimEnd('\') + '\'
Get-Process |
    Where-Object { $_.Path -and $_.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object {
        Write-Host "Cerrando $($_.ProcessName) (PID $($_.Id)), que se ejecuta desde publish ..."
        Stop-Process -Id $_.Id -Force
    }
$ErrorActionPreference = $previousPreference
Start-Sleep -Milliseconds 700

# 2) Borra la carpeta anterior, con reintentos. Si algo (Explorador, antivirus, otro
#    programa) la sigue bloqueando, se publica en una carpeta temporal en vez de fallar.
if (Test-Path $publishDir) {
    Write-Host "Eliminando carpeta de publicacion: $publishDir"

    $deleted = $false
    for ($i = 1; $i -le 5; $i++) {
        try {
            Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction Stop
            $deleted = $true
            break
        }
        catch {
            Write-Host "Carpeta bloqueada. Reintentando ($i/5)..."
            Start-Sleep -Seconds 2
        }
    }

    if (-not $deleted) {
        $stamp = Get-Date -Format "yyyyMMddHHmmss"
        $publishDir = Join-Path $env:TEMP "LauncherPublish-$version-$stamp"
        Write-Host "No se pudo borrar 'publish'. Se publica en: $publishDir"
    }
}

Write-Host "Publicando Launcher $version ..."
dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo" }

Write-Host "Empaquetando con Velopack en $Releases ..."
vpk pack --packId Launcher --packVersion $version --packDir $publishDir --mainExe Launcher.exe --runtime win-x64 --outputDir $Releases
if ($LASTEXITCODE -ne 0) { throw "vpk pack fallo" }

Write-Host ""
Write-Host "Listo. Version $version publicada en: $Releases"
Write-Host "Primera vez: instala con el Setup.exe de esa carpeta."
Write-Host "Despues, las copias instaladas detectan las versiones nuevas solas."