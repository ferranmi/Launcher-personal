
# ============================================================
# LAUNCHER - REPOSITORY INVENTORY GENERATOR
# ============================================================

$ErrorActionPreference = "Stop"

# Directorio del repositorio
$root = $PSScriptRoot

if (-not $root) {
    $root = (Get-Location).Path
}

$output = Join-Path $root "_AUDIT_INVENTORY"

New-Item -ItemType Directory -Path $output -Force |
    Out-Null

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " LAUNCHER - INVENTORY GENERATOR" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Repository: $root"
Write-Host ""

# ------------------------------------------------------------
# 1. REPOSITORY TREE
# ------------------------------------------------------------

Write-Host "[1/7] Generating repository tree..." -ForegroundColor Yellow

$excluded = '\\(\.git|bin|obj|\.vs|node_modules)\\'

Get-ChildItem -Path $root -Recurse -File -Force |
    Where-Object {
        $_.FullName -notmatch $excluded -and
        $_.DirectoryName -notmatch $excluded -and
        $_.FullName -notmatch '\\_AUDIT_INVENTORY\\'
    } |
    ForEach-Object {
        $_.FullName.Substring($root.Length + 1)
    } |
    Sort-Object |
    Out-File "$output\REPOSITORY_TREE.txt" -Encoding utf8

# ------------------------------------------------------------
# 2. CORE INVENTORY
# ------------------------------------------------------------

Write-Host "[2/7] Generating Core inventory..." -ForegroundColor Yellow

$core = Join-Path $root "Core"

if (Test-Path $core) {

    Get-ChildItem $core -Recurse -Filter "*.cs" -File |
        Select-Object @{
            Name = "File"
            Expression = {
                $_.FullName.Substring($root.Length + 1)
            }
        }, Length |
        Format-Table -AutoSize |
        Out-String |
        Out-File "$output\CORE_INVENTORY.txt" -Encoding utf8

} else {
    "Core directory not found." |
        Out-File "$output\CORE_INVENTORY.txt" -Encoding utf8
}

# ------------------------------------------------------------
# 3. PROJECT CONFIGURATION
# ------------------------------------------------------------

Write-Host "[3/7] Extracting project configuration..." -ForegroundColor Yellow

Get-ChildItem $root -Recurse -Filter "*.csproj" -File |
    Where-Object { $_.FullName -notmatch $excluded } |
    ForEach-Object {
        "`n===== $($_.FullName.Substring($root.Length + 1)) ====="
        Get-Content $_.FullName
    } |
    Out-File "$output\PROJECT_CONFIGURATION.txt" -Encoding utf8

# ------------------------------------------------------------
# 4. C# FILE INVENTORY
# ------------------------------------------------------------

Write-Host "[4/7] Generating C# inventory..." -ForegroundColor Yellow

Get-ChildItem $root -Recurse -Filter "*.cs" -File |
    Where-Object {
        $_.FullName -notmatch $excluded
    } |
    ForEach-Object {
        [PSCustomObject]@{
            File = $_.FullName.Substring($root.Length + 1)
            Size = $_.Length
            Lines = (Get-Content $_.FullName | Measure-Object -Line).Lines
        }
    } |
    Export-Csv "$output\CS_INVENTORY.csv" -NoTypeInformation -Encoding utf8

# ------------------------------------------------------------
# 5. XAML INVENTORY
# ------------------------------------------------------------

Write-Host "[5/7] Generating XAML inventory..." -ForegroundColor Yellow

Get-ChildItem $root -Recurse -Filter "*.xaml" -File |
    Where-Object {
        $_.FullName -notmatch $excluded
    } |
    ForEach-Object {
        [PSCustomObject]@{
            File = $_.FullName.Substring($root.Length + 1)
            Size = $_.Length
        }
    } |
    Export-Csv "$output\XAML_INVENTORY.csv" -NoTypeInformation -Encoding utf8

# ------------------------------------------------------------
# 6. GIT STATUS
# ------------------------------------------------------------

Write-Host "[6/7] Extracting Git status..." -ForegroundColor Yellow

if (Test-Path (Join-Path $root ".git")) {

    git -C $root status --short --branch |
        Out-File "$output\GIT_STATUS.txt" -Encoding utf8

    git -C $root log -1 --format="%H%n%ad%n%s" --date=iso |
        Out-File "$output\GIT_LAST_COMMIT.txt" -Encoding utf8

} else {

    "Git repository not detected." |
        Out-File "$output\GIT_STATUS.txt" -Encoding utf8
}

# ------------------------------------------------------------
# 7. SOLUTION AND TEST FILES
# ------------------------------------------------------------

Write-Host "[7/7] Locating solutions and tests..." -ForegroundColor Yellow

Get-ChildItem $root -Recurse -File |
    Where-Object {
        $_.FullName -notmatch $excluded -and
        ($_.Extension -in @(".sln", ".slnx") -or
         $_.Name -match '(test|tests|spec)' )
    } |
    ForEach-Object {
        $_.FullName.Substring($root.Length + 1)
    } |
    Sort-Object |
    Out-File "$output\SOLUTIONS_AND_TESTS.txt" -Encoding utf8

# ------------------------------------------------------------
# FINISHED
# ------------------------------------------------------------

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " INVENTORY GENERATED SUCCESSFULLY" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output directory:" -ForegroundColor Cyan
Write-Host $output -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
