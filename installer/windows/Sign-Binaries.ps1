<#
.SYNOPSIS
    Signs all .exe and .dll files in a publish directory using signtool.exe.
    Called by the GitHub Actions release workflow.

.PARAMETERS
    PublishDir   — root directory containing the files to sign
    CertPfxPath  — path to the PFX certificate file
    CertPassword — PFX password
    TimestampUrl — RFC 3161 timestamp server URL
#>

param(
    [Parameter(Mandatory)] [string] $PublishDir,
    [Parameter(Mandatory)] [string] $CertPfxPath,
    [Parameter(Mandatory)] [string] $CertPassword,
    [string] $TimestampUrl = "http://timestamp.digicert.com",
    [string] $Description  = "MonaServer2 GUI"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Locate signtool.exe ─────────────────────────────────────────────────────
function Find-SignTool {
    $sdkBase = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $sdkBase) {
        $tool = Get-ChildItem -Path $sdkBase -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match "x64" } |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1 -ExpandProperty FullName
        if ($tool) { return $tool }
    }
    # Fall back to PATH
    $fromPath = (Get-Command signtool.exe -ErrorAction SilentlyContinue)?.Source
    if ($fromPath) { return $fromPath }
    throw "signtool.exe not found. Install the Windows SDK."
}

$signtool = Find-SignTool
Write-Host "Using signtool: $signtool" -ForegroundColor Cyan

# ── Collect targets ─────────────────────────────────────────────────────────
$targets = Get-ChildItem -Path $PublishDir -Recurse -Include "*.exe","*.dll" |
           Where-Object { -not $_.FullName.Contains("ref\") }

if ($targets.Count -eq 0) {
    Write-Warning "No .exe or .dll files found in $PublishDir"
    exit 0
}

Write-Host "Signing $($targets.Count) file(s)..." -ForegroundColor Cyan

# ── Sign each file ──────────────────────────────────────────────────────────
$failed = 0
foreach ($file in $targets) {
    Write-Host "  Signing: $($file.Name)" -NoNewline

    $args = @(
        "sign",
        "/fd",  "SHA256",
        "/td",  "SHA256",
        "/tr",  $TimestampUrl,
        "/f",   $CertPfxPath,
        "/p",   $CertPassword,
        "/d",   $Description,
        "/du",  "https://github.com/mehdimahdian/MonaServer2-GUI",
        $file.FullName
    )

    $result = & $signtool @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host " FAILED" -ForegroundColor Red
        Write-Host $result
        $failed++
    } else {
        Write-Host " OK" -ForegroundColor Green
    }
}

if ($failed -gt 0) {
    throw "$failed file(s) failed to sign."
}

Write-Host "All files signed successfully." -ForegroundColor Green
