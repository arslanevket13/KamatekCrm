[CmdletBinding()]
param(
    [string]$PgVersion = "16.8-1",
    [string]$DownloadUrl = "",
    [switch]$ForceDownload,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repoRoot = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $repoRoot "KamatekCRM\PostgresRuntime"

$requiredChecks = @(
    "bin\initdb.exe",
    "bin\pg_ctl.exe",
    "bin\postgres.exe",
    "bin\pg_isready.exe",
    "share\postgres.bki"
)

function Test-RuntimeComplete([string]$RootPath) {
    foreach ($rel in $requiredChecks) {
        if (-not (Test-Path (Join-Path $RootPath $rel))) {
            return $false
        }
    }
    return $true
}

function Copy-MinimalPostgresRuntime([string]$SourceRoot, [string]$DestinationRoot) {
    $dstBin = Join-Path $DestinationRoot "bin"
    $dstLib = Join-Path $DestinationRoot "lib"
    $dstShare = Join-Path $DestinationRoot "share"

    New-Item -ItemType Directory -Force -Path $dstBin, $dstLib, $dstShare | Out-Null

    $exes = @(
        "initdb.exe",
        "pg_ctl.exe",
        "postgres.exe",
        "pg_isready.exe",
        "pg_dump.exe",
        "pg_restore.exe",
        "psql.exe"
    )

    $srcBin = Join-Path $SourceRoot "bin"
    foreach ($exe in $exes) {
        $srcFile = Join-Path $srcBin $exe
        if (Test-Path $srcFile) {
            Copy-Item -Path $srcFile -Destination $dstBin -Force
        }
    }

    Get-ChildItem -Path $srcBin -Filter "*.dll" -File -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $dstBin -Force
    }

    $srcLib = Join-Path $SourceRoot "lib"
    if (Test-Path $srcLib) {
        Get-ChildItem -Path $srcLib -Filter "*.dll" -File -ErrorAction SilentlyContinue | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $dstLib -Force
        }
    }

    $srcShare = Join-Path $SourceRoot "share"
    if (Test-Path $srcShare) {
        Copy-Item -Path (Join-Path $srcShare "*") -Destination $dstShare -Recurse -Force
    }
}

if (-not $Force -and -not $ForceDownload -and (Test-RuntimeComplete $targetDir)) {
    $existingCount = (Get-ChildItem -Path $targetDir -Recurse -File).Count
    Write-Host "[OK] PostgresRuntime zaten hazir ($existingCount dosya): $targetDir"
    exit 0
}

$sourceRoot = $null
$tempRoot = $null

try {
    if (-not $ForceDownload) {
        $searchRoots = @("C:\Program Files\PostgreSQL", "C:\Program Files (x86)\PostgreSQL")
        $candidates = @()
        foreach ($sr in $searchRoots) {
            if (Test-Path $sr) {
                Get-ChildItem -Path $sr -Directory -ErrorAction SilentlyContinue | ForEach-Object {
                    $dir = $_
                    $hasAllBins = (Test-Path (Join-Path $dir.FullName "bin\initdb.exe")) -and
                                  (Test-Path (Join-Path $dir.FullName "bin\pg_ctl.exe")) -and
                                  (Test-Path (Join-Path $dir.FullName "bin\postgres.exe")) -and
                                  (Test-Path (Join-Path $dir.FullName "bin\pg_isready.exe"))
                    if ($hasAllBins) {
                        $digits = ($dir.Name -replace '\D', '')
                        $verNum = if ([string]::IsNullOrEmpty($digits)) { 0 } else { [int]$digits }
                        $candidates += [PSCustomObject]@{ Path = $dir.FullName; Version = $verNum }
                    }
                }
            }
        }

        $bestLocal = $candidates | Sort-Object -Property Version -Descending | Select-Object -First 1
        if ($bestLocal) {
            $sourceRoot = $bestLocal.Path
            Write-Host "[LOCAL] Kurulu PostgreSQL bulundu: $sourceRoot"
        }
    }

    if (-not $sourceRoot) {
        if ([string]::IsNullOrWhiteSpace($DownloadUrl)) {
            $DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-$PgVersion-windows-x64-binaries.zip"
        }

        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("kamatek_pg_" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
        $zipPath = Join-Path $tempRoot "pgsql.zip"
        $extractDir = Join-Path $tempRoot "extracted"
        New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

        Write-Host "[DOWNLOAD] PostgreSQL $PgVersion indiriliyor: $DownloadUrl"
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $zipPath -UseBasicParsing

        Write-Host "[EXTRACT] Arsiv aciliyor..."
        Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

        $pgsqlDir = Join-Path $extractDir "pgsql"
        if (-not (Test-Path (Join-Path $pgsqlDir "bin\postgres.exe"))) {
            $found = Get-ChildItem -Path $extractDir -Recurse -Filter "postgres.exe" -File | Select-Object -First 1
            if (-not $found) {
                throw "Indirilen arsivde bin\postgres.exe bulunamadi."
            }
            $pgsqlDir = Split-Path -Parent (Split-Path -Parent $found.FullName)
        }
        $sourceRoot = $pgsqlDir
    }

    Copy-MinimalPostgresRuntime -SourceRoot $sourceRoot -DestinationRoot $targetDir

    foreach ($req in $requiredChecks) {
        $reqPath = Join-Path $targetDir $req
        if (-not (Test-Path $reqPath)) {
            Write-Error "Zorunlu dosya eksik: $reqPath"
            exit 1
        }
    }

    $fileCount = (Get-ChildItem -Path $targetDir -Recurse -File).Count
    $totalMb = [Math]::Round(((Get-ChildItem -Path $targetDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
    Write-Host "[TAMAM] PostgresRuntime hazir: $fileCount dosya ($totalMb MB) -> $targetDir"
}
finally {
    if ($tempRoot -and (Test-Path $tempRoot)) {
        Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
