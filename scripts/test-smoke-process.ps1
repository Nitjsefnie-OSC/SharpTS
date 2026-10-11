[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SmokeProcess.psm1') -Force
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "sharpts-process-smoke-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $testRoot | Out-Null
$fixture = Join-Path $testRoot 'process.ps1'
$childPidFile = Join-Path $testRoot 'child.pid'
$pwsh = [Environment]::ProcessPath
@'
param([string] $Mode, [string] $ChildPidFile)
switch ($Mode) {
    'success' { [Console]::WriteLine('smoke-ok'); [Console]::Error.WriteLine('diagnostic'); exit 0 }
    'failure' { [Console]::Error.WriteLine('fixture failure'); exit 7 }
    'hang' { Start-Sleep -Seconds 30; exit 0 }
    'inherited-pipe' {
        $start = [Diagnostics.ProcessStartInfo]::new([Environment]::ProcessPath)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        foreach ($argument in @('-NoProfile', '-Command', 'Start-Sleep -Seconds 30')) { $start.ArgumentList.Add($argument) }
        $child = [Diagnostics.Process]::Start($start)
        $child.Id | Set-Content -LiteralPath $ChildPidFile
        [Console]::WriteLine('parent returned')
        exit 0
    }
}
'@ | Set-Content -LiteralPath $fixture -Encoding utf8NoBOM
$options = @{ FileName = $pwsh; WorkingDirectory = $testRoot; LogDirectory = $testRoot }

function Assert-Throws([scriptblock] $Action, [string] $Expected) {
    try { & $Action }
    catch {
        if ($_.Exception.Message -notlike "*$Expected*") { throw "Expected '$Expected', got '$($_.Exception.Message)'." }
        return
    }
    throw "Expected '$Expected'."
}

try {
    $text = Invoke-SmokeProcess -Name success -Arguments @('-NoProfile', '-File', $fixture, 'success') @options
    if ($text -cne 'smoke-ok') { throw 'Successful process output was not captured.' }
    if ((Get-Content -LiteralPath (Join-Path $testRoot 'success.log') -Raw) -notmatch 'diagnostic') {
        throw 'Successful process stderr was not preserved.'
    }
    Assert-Throws { Invoke-SmokeProcess -Name failure -Arguments @('-NoProfile', '-File', $fixture, 'failure') @options } 'exit code 7'
    Assert-Throws { Invoke-SmokeProcess -Name hang -Arguments @('-NoProfile', '-File', $fixture, 'hang') -TimeoutMilliseconds 2000 @options } 'exceeded 2000 milliseconds'
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    Assert-Throws {
        Invoke-SmokeProcess -Name inherited-pipe -Arguments @('-NoProfile', '-File', $fixture, 'inherited-pipe', $childPidFile) `
            -TimeoutMilliseconds 2000 @options
    } 'exceeded 2000 milliseconds'
    $elapsed.Stop()
    if ($elapsed.ElapsedMilliseconds -ge 7000) { throw 'Inherited pipes made the process runner exceed its bounded cleanup window.' }
    $log = Get-Content -LiteralPath (Join-Path $testRoot 'inherited-pipe.log') -Raw
    if ($log -notmatch 'Exit code: 0' -or $log -notmatch 'inherited output pipes exceeded the deadline') {
        throw 'The timeout did not retain evidence that the parent exited while a descendant retained its output pipes.'
    }
    Write-Host 'Passed process smoke checks: output, nonzero exit, timeout cleanup, and inherited pipe deadline.'
}
finally {
    if (Test-Path -LiteralPath $childPidFile) {
        $childPid = [int] (Get-Content -LiteralPath $childPidFile)
        Stop-Process -Id $childPid -Force -ErrorAction SilentlyContinue
    }
    # Retain the tiny fixture and logs in the temp directory for diagnosis.
}
