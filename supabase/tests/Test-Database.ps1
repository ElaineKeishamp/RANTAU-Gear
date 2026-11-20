param(
    [string]$AdditionalMigration,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [string]$OutputRoot = 'D:\elaine\SEM 5\Pemrograman Visual-backup'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (![IO.Path]::IsPathFullyQualified($OutputRoot) -or [IO.Path]::GetPathRoot($OutputRoot) -eq 'C:\') {
    throw 'Use an absolute validation output directory outside drive C.'
}
$runRoot = Join-Path $OutputRoot ('RANTAUGear-db-check-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $runRoot | Out-Null
$clusterPath = Join-Path $runRoot 'data'
$psql = Join-Path $PostgresBin 'psql.exe'
$pgCtl = Join-Path $PostgresBin 'pg_ctl.exe'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$serverStarted = $false
$raceProcess = $null
function Invoke-TestSql([string]$Path) {
    $log = Join-Path $runRoot ([IO.Path]::GetFileName($Path)+'.log')
    & $psql -X -w -h 127.0.0.1 -p $port -U rantau_test -d rantau_validation -v ON_ERROR_STOP=1 -f $Path *> $log
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 20; throw "SQL failed: $Path" }
    $checks = @(Select-String -LiteralPath $log -Pattern 'NOTICE:  PASS:').Count
    Write-Output ("PASS: {0} ({1} checks)" -f [IO.Path]::GetFileName($Path),$checks)
}
try {
    & (Join-Path $PostgresBin 'initdb.exe') -D $clusterPath -U rantau_test --auth=trust --encoding=UTF8 --locale=C *> (Join-Path $runRoot 'initdb.log')
    if ($LASTEXITCODE -ne 0) { throw 'initdb failed; inspect initdb.log.' }
    $serverProcess = Start-Process -FilePath (Join-Path $PostgresBin 'postgres.exe') -ArgumentList @('-D',('"'+$clusterPath+'"'),'-h','127.0.0.1','-p',$port) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'postgres.stdout.log') -RedirectStandardError (Join-Path $runRoot 'postgres.stderr.log')
    $serverStarted = $true
    $ready = $false
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        & (Join-Path $PostgresBin 'pg_isready.exe') -h 127.0.0.1 -p $port -U rantau_test *> $null
        if ($LASTEXITCODE -eq 0) { $ready=$true; break }
        Start-Sleep -Milliseconds 200
    }
    if (!$ready) { throw 'Isolated PostgreSQL did not become ready.' }
    & $psql -X -w -h 127.0.0.1 -p $port -U rantau_test -d postgres -v ON_ERROR_STOP=1 -c 'create database rantau_validation' *> (Join-Path $runRoot 'create-db.log')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create test database.' }
    Invoke-TestSql (Join-Path $PSScriptRoot 'bootstrap_local.sql')
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'supabase\migrations') -Filter '*.sql' | Sort-Object Name | ForEach-Object { Invoke-TestSql $_.FullName }
    if ($AdditionalMigration) { Invoke-TestSql $AdditionalMigration }
    Invoke-TestSql (Join-Path $PSScriptRoot 'reservations.sql')
    Invoke-TestSql (Join-Path $PSScriptRoot 'concurrency_setup.sql')
    $raceSql=Join-Path $PSScriptRoot 'concurrent_reservation.sql'
    $connection="host=127.0.0.1 port=$port user=rantau_test dbname=rantau_validation application_name=rantau_race_a"
    $raceProcess=Start-Process -FilePath $psql -ArgumentList @('-X','-w','-d',('"'+$connection+'"'),'-v','ON_ERROR_STOP=1','-v','request_id=aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','-v','hold_lock=true','-f',('"'+$raceSql+'"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'race-a.out') -RedirectStandardError (Join-Path $runRoot 'race-a.err')
    $holdingLock=$false
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        $waiting=& $psql -X -w -At -h 127.0.0.1 -p $port -U rantau_test -d rantau_validation -c "select count(*) from pg_stat_activity where application_name='rantau_race_a' and wait_event='PgSleep'"
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect concurrency test.' }
        if ($waiting -eq '1') { $holdingLock=$true; break }
        Start-Sleep -Milliseconds 100
    }
    if (!$holdingLock) { throw 'First reservation did not hold lock as expected.' }
    & $psql -X -w -h 127.0.0.1 -p $port -U rantau_test -d rantau_validation -v ON_ERROR_STOP=1 -v request_id=bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb -v hold_lock=false -f $raceSql *> (Join-Path $runRoot 'race-b.log')
    if ($LASTEXITCODE -eq 0 -or !(Select-String -LiteralPath (Join-Path $runRoot 'race-b.log') -Pattern 'Stok .* tidak cukup')) { throw 'Concurrent overbooking was not rejected correctly.' }
    $raceProcess.WaitForExit()
    if ($raceProcess.ExitCode -ne 0) { throw 'First concurrent reservation failed.' }
    $count=& $psql -X -w -At -h 127.0.0.1 -p $port -U rantau_test -d rantau_validation -c "select count(*) from public.rentals r join public.customers c on c.id=r.customer_id where c.name='Race Customer'"
    if ($LASTEXITCODE -ne 0 -or $count -ne '1') { throw 'Concurrency test saved an unexpected number of rentals.' }
    Write-Output 'PASS: Two concurrent connections cannot reserve the last unit twice.'
    Write-Output "Database validation complete. Logs: $runRoot"
}
finally {
    if ($raceProcess -and !$raceProcess.HasExited) { $raceProcess.WaitForExit(10000) | Out-Null }
    if ($serverStarted) {
        & $pgCtl -D $clusterPath -m fast -w stop *> (Join-Path $runRoot 'shutdown.log')
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not stop isolated test cluster at $clusterPath" }
    }
}
