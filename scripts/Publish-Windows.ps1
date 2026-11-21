[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath('D:\elaine\SEM 5\Pemrograman Visual-backup')
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $outputRoot ('RANTAUGear-publish-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
if (!$releaseRoot.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output outside authorized backup folder.' }
if (Test-Path -LiteralPath $releaseRoot) { throw 'Release output already exists.' }
$config = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'supabase.local.json') | ConvertFrom-Json
$projectUri = [Uri]$config.projectUrl
if (!$projectUri.IsAbsoluteUri -or $projectUri.Scheme -ne 'https' -or $projectUri.AbsolutePath -ne '/' -or $projectUri.UserInfo -or $projectUri.Query -or $projectUri.Fragment) { throw 'Invalid public project URL.' }
if (!$config.publishableKey -or !$config.publishableKey.StartsWith('sb_publishable_') -or $config.publishableKey -match '\s') { throw 'Only a public publishable key may enter the package.' }
$package = Join-Path $releaseRoot 'RANTAU Gear'
$cache = Join-Path $outputRoot 'RANTAUGear-publish-cache'
New-Item -ItemType Directory -Path $package,$cache,(Join-Path $cache 'tmp') -Force | Out-Null
$env:DOTNET_CLI_HOME = Join-Path $cache 'dotnet'
$env:NUGET_PACKAGES = Join-Path $cache 'packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:TEMP = Join-Path $cache 'tmp'
$env:TMP = $env:TEMP
$log = Join-Path $releaseRoot 'publish.log'
& dotnet publish (Join-Path $projectRoot 'RANTAUGear\RANTAUGear.vbproj') -c Release -r win-x64 --self-contained true --artifacts-path (Join-Path $releaseRoot 'build') -o $package -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false *> $log
if ($LASTEXITCODE -ne 0) { Get-Content $log -Tail 24; throw 'Publish failed; inspect publish.log.' }
[ordered]@{ projectUrl = $config.projectUrl; publishableKey = $config.publishableKey } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'supabase.local.json') -Encoding utf8
$instructions = @(
 'RANTAU Gear - Windows 64-bit',
 '1. Ekstrak seluruh ZIP ke folder yang bisa ditulis, misalnya Documents atau Desktop.',
 '2. Buka RANTAUGear.exe di dalam folder RANTAU Gear.',
 '3. Masuk dengan akun staf aktif. Internet tetap diperlukan.',
 'Tidak perlu Visual Studio atau instalasi .NET terpisah. Simpan seluruh isi folder bersama-sama.',
 'Ekspor CSV disimpan pada subfolder Laporan di sebelah aplikasi.',
 'supabase.local.json hanya memuat URL dan publishable key publik. Jangan menambahkan password, secret key, service_role key, atau token.',
 'Tidak ada akun/password demo. Mintalah akun kepada Super Admin toko.',
 'Paket ini belum memiliki installer atau tanda tangan sertifikat penerbit.'
)
$instructions | Set-Content -LiteralPath (Join-Path $package 'CARA MENJALANKAN.txt') -Encoding utf8
$zip = Join-Path $releaseRoot 'RANTAU-Gear-win-x64.zip'
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash + '  RANTAU-Gear-win-x64.zip' | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ascii
[ordered]@{ createdAt = (Get-Date).ToString('o'); runtime = 'win-x64'; selfContained = $true; sourceCommit = (& git -C $projectRoot rev-parse HEAD); workingTreeDirty = [bool](& git -C $projectRoot status --porcelain); package = $package; zip = $zip } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseRoot 'release-info.json') -Encoding utf8
Write-Output "PUBLISH_ROOT=$releaseRoot"
Write-Output "PACKAGE=$package"
Write-Output "ZIP=$zip"
