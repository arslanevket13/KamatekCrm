<#
.SYNOPSIS
    KamatekCRM için minimal Windows x64 portable PostgreSQL çalışma zamanını indirir ve çıkarır.
.DESCRIPTION
    PostgreSQL portable ZIP arşivinden yalnızca yerel kurulum ve yedekleme için gerekli dosyaları
    (bin/initdb.exe, pg_ctl.exe, postgres.exe, pg_isready.exe, pg_dump.exe, pg_restore.exe,
    gerekli DLL'ler, lib/ ve share/) KamatekCRM/PostgresRuntime/ klasörüne yerleştirir.
    Hedef klasörde gerekli dosyalar zaten mevcutsa indirmeyi atlar (-Force ile zorlanabilir).
#>
[CmdletBinding()]
param(
    [string]$PgVersion = "16.8-1",
    [string]$DownloadUrl = "",
    [string]$TargetDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "KamatekCRM\PostgresRuntime"),
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$requiredExes = @(
    "initdb.exe",
    "pg_ctl.exe",
    "postgres.exe",
    "pg_isready.exe",
    "pg_dump.exe",
    "pg_restore.exe"
)

$targetBin = Join-Path $TargetDir "bin"
$alreadyReady = $true
foreach ($exe in $requiredExes) {
    if (-not (Test-Path (Join-Path $targetBin $exe))) {
        $alreadyReady = $false
        break
    }
}

if ($alreadyReady -and -not $Force) {
    Write-Host "[OK] PostgresRuntime zaten mevcut: $TargetDir (yeniden indirmek için -Force kullanın)." -ForegroundColor Green
    exit 0
}

if ([string]::IsNullOrWhiteSpace($DownloadUrl)) {
    $DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-$PgVersion-windows-x64-binaries.zip"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("kamatek_pg_" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
$zipPath = Join-Path $tempRoot "pgsql.zip"
$extractDir = Join-Path $tempRoot "extracted"

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    Write-Host "[1/3] PostgreSQL $PgVersion portable arşivi indiriliyor..." -ForegroundColor Cyan
    Write-Host "      URL: $DownloadUrl"
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $zipPath -UseBasicParsing

    Write-Host "[2/3] Arşiv açılıyor..." -ForegroundColor Cyan
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

    $pgsqlRoot = Join-Path $extractDir "pgsql"
    if (-not (Test-Path (Join-Path $pgsqlRoot "bin\postgres.exe"))) {
        throw "Beklenen 'pgsql/bin/postgres.exe' dizin yapısı arşivde bulunamadı."
    }

    Write-Host "[3/3] Minimal çalışma zamanı kopyalanıyor -> $TargetDir" -ForegroundColor Cyan
    if (Test-Path $TargetDir) {
        Remove-Item -Path $TargetDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $targetBin -Force | Out-Null

    # Yalnızca gerekli exe'ler + tüm bağımlı DLL'ler
    foreach ($exe in ($requiredExes + @("psql.exe"))) {
        $src = Join-Path $pgsqlRoot "bin\$exe"
        if (Test-Path $src) {
            Copy-Item -Path $src -Destination $targetBin -Force
        }
    }
    Get-ChildItem -Path (Join-Path $pgsqlRoot "bin") -Filter "*.dll" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $targetBin -Force
    }

    # lib/ (PostgreSQL çekirdek uzantı ve kodlama DLL'leri)
    $srcLib = Join-Path $pgsqlRoot "lib"
    $dstLib = Join-Path $TargetDir "lib"
    New-Item -ItemType Directory -Path $dstLib -Force | Out-Null
    Get-ChildItem -Path $srcLib -Filter "*.dll" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $dstLib -Force
    }

    # share/ (initdb ve UTF-8/timezone verileri için zorunlu)
    $srcShare = Join-Path $pgsqlRoot "share"
    $dstShare = Join-Path $TargetDir "share"
    Copy-Item -Path $srcShare -Destination $dstShare -Recurse -Force

    $totalMb = [Math]::Round(((Get-ChildItem -Path $TargetDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
    Write-Host "[TAMAM] PostgresRuntime hazır ($totalMb MB): $TargetDir" -ForegroundColor Green
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
