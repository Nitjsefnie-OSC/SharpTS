[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $AdapterPath,
    [string[]] $AdapterArguments = @(),
    [Parameter(Mandatory)] [string] $ResultsDirectory,
    [ValidateRange(1, 180)] [int] $TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$resultsRoot = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
$program = Join-Path $resultsRoot 'main.ts'
@'
import { addTwo } from "./helper";
const seed = 40;
const answer = addTwo(seed);
console.log(`packaged-dap=${answer}`);
'@ | Set-Content -LiteralPath $program -Encoding utf8NoBOM
'export function addTwo(value: number): number { return value + 2; }' |
    Set-Content -LiteralPath (Join-Path $resultsRoot 'helper.ts') -Encoding utf8NoBOM
$transcriptPath = Join-Path $resultsRoot 'dap-transcript.ndjson'
'' | Set-Content -LiteralPath $transcriptPath -NoNewline -Encoding utf8NoBOM
$state = @{
    Sequence = 0
    Deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    Events = [Collections.Generic.List[object]]::new()
    Messages = [Collections.Generic.List[object]]::new()
}
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $AdapterPath
$start.WorkingDirectory = $resultsRoot
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in $AdapterArguments) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
$captureCancellation = [Threading.CancellationTokenSource]::new()
$result = [ordered]@{ status = 'failed'; adapter = $AdapterPath; transcript = $transcriptPath }
$standardError = $null
$started = $false

function Get-RemainingMilliseconds {
    $remaining = [int] [Math]::Ceiling(($state.Deadline - [DateTime]::UtcNow).TotalMilliseconds)
    if ($remaining -le 0) { throw "Installed adapter protocol smoke exceeded $TimeoutSeconds seconds." }
    return $remaining
}

function Add-Transcript([string] $Direction, [object] $Message) {
    $entry = [ordered]@{ direction = $Direction; message = $Message }
    $state.Messages.Add($entry)
    [IO.File]::AppendAllText($transcriptPath, ($entry | ConvertTo-Json -Depth 30 -Compress) + "`n")
}

function Read-Bytes([byte[]] $Buffer, [int] $Offset, [int] $Count) {
    while ($Count -gt 0) {
        $task = $process.StandardOutput.BaseStream.ReadAsync($Buffer, $Offset, $Count, $captureCancellation.Token)
        if (-not $task.Wait((Get-RemainingMilliseconds))) {
            throw "Installed adapter protocol smoke exceeded $TimeoutSeconds seconds while reading DAP."
        }
        $read = $task.GetAwaiter().GetResult()
        if ($read -eq 0) { throw 'Installed adapter closed protocol stdout before completing the smoke.' }
        $Offset += $read
        $Count -= $read
    }
}

function Read-Message {
    $header = [Collections.Generic.List[byte]]::new()
    $delimiter = [Text.Encoding]::ASCII.GetBytes("`r`n`r`n")
    $matched = 0
    $single = [byte[]]::new(1)
    while ($matched -ne 4) {
        Read-Bytes $single 0 1
        $header.Add($single[0])
        if ($header.Count -gt 8192) { throw 'Installed adapter sent an oversized or unframed DAP header.' }
        $matched = if ($single[0] -eq $delimiter[$matched]) { $matched + 1 }
            elseif ($single[0] -eq $delimiter[0]) { 1 } else { 0 }
    }
    $headerText = [Text.Encoding]::ASCII.GetString($header.ToArray())
    $lengths = [regex]::Matches($headerText, '(?im)^Content-Length:\s*(\d+)\r?$')
    if ($lengths.Count -ne 1) { throw 'Installed adapter sent no unique DAP Content-Length.' }
    $length = [int] $lengths[0].Groups[1].Value
    if ($length -le 0 -or $length -gt 1048576) { throw 'Installed adapter sent an invalid DAP payload length.' }
    $payload = [byte[]]::new($length)
    Read-Bytes $payload 0 $length
    $message = [Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json -AsHashtable
    Add-Transcript 'adapter' $message
    return $message
}

function Request([string] $Command, [hashtable] $Arguments = @{}) {
    $state.Sequence++
    $message = @{ seq = $state.Sequence; type = 'request'; command = $Command; arguments = $Arguments }
    Add-Transcript 'client' $message
    $payload = [Text.Encoding]::UTF8.GetBytes(($message | ConvertTo-Json -Depth 15 -Compress))
    $header = [Text.Encoding]::ASCII.GetBytes("Content-Length: $($payload.Length)`r`n`r`n")
    $inputStream = $process.StandardInput.BaseStream
    foreach ($bytes in @($header, $payload)) {
        if (-not $inputStream.WriteAsync($bytes, 0, $bytes.Length, $captureCancellation.Token).Wait((Get-RemainingMilliseconds))) {
            throw "Installed adapter protocol smoke exceeded $TimeoutSeconds seconds while sending $Command."
        }
    }
    if (-not $inputStream.FlushAsync($captureCancellation.Token).Wait((Get-RemainingMilliseconds))) { throw 'DAP flush exceeded the deadline.' }
    while ($true) {
        $response = Read-Message
        if ($response.type -eq 'event') { $state.Events.Add($response); continue }
        if ($response.type -ne 'response' -or $response.request_seq -ne $state.Sequence -or $response.command -cne $Command) {
            throw "Installed adapter returned an uncorrelated response to $Command."
        }
        if ($response.success -ne $true) { throw "Installed adapter rejected ${Command}: $($response.message)" }
        return $response
    }
}

function Wait-Event([string] $Name) {
    for ($index = 0; $index -lt $state.Events.Count; $index++) {
        if ($state.Events[$index].event -ceq $Name) {
            $event = $state.Events[$index]
            $state.Events.RemoveAt($index)
            return $event
        }
    }
    while ($true) {
        $message = Read-Message
        if ($message.type -ne 'event') { throw "Installed adapter sent a response while waiting for $Name." }
        if ($message.event -ceq $Name) { return $message }
        $state.Events.Add($message)
    }
}

try {
    $started = $process.Start()
    if (-not $started) { throw 'Could not start the installed adapter shim.' }
    $standardError = $process.StandardError.ReadToEndAsync($captureCancellation.Token)
    $initialize = Request 'initialize' @{ adapterID = 'sharpts'; linesStartAt1 = $true; columnsStartAt1 = $true }
    if ($initialize.body.supportsConfigurationDoneRequest -ne $true) { throw 'Installed adapter lacks configurationDone support.' }
    $null = Wait-Event 'initialized'
    $null = Request 'launch' @{ program = $program; cwd = $resultsRoot; console = 'internalConsole' }
    $breakpoints = Request 'setBreakpoints' @{ source = @{ path = $program }; breakpoints = @(@{ line = 4 }) }
    if ($breakpoints.body.breakpoints.Count -ne 1 -or $breakpoints.body.breakpoints[0].verified -ne $true) {
        throw 'Installed adapter did not verify the source breakpoint.'
    }
    $null = Request 'configurationDone'
    $stopped = Wait-Event 'stopped'
    if ($stopped.body.reason -cne 'breakpoint') { throw 'Installed adapter did not stop at the source breakpoint.' }
    $stack = Request 'stackTrace' @{ threadId = $stopped.body.threadId }
    $frame = $stack.body.stackFrames[0]
    if ($frame.line -ne 4 -or [IO.Path]::GetFullPath($frame.source.path) -ne $program) {
        throw 'Installed adapter returned the wrong breakpoint source location.'
    }
    $evaluation = Request 'evaluate' @{ frameId = $frame.id; expression = 'answer'; context = 'watch' }
    if ($evaluation.body.result -cne '42') { throw 'Installed adapter did not evaluate the imported function result.' }
    $null = Request 'continue' @{ threadId = $stopped.body.threadId }
    $exited = Wait-Event 'exited'
    if ($exited.body.exitCode -ne 0) { throw 'Packaged interpreter program failed.' }
    $null = Wait-Event 'terminated'
    $output = ($state.Messages | Where-Object { $_.direction -eq 'adapter' -and $_.message.type -eq 'event' -and $_.message.event -eq 'output' } |
        ForEach-Object { $_.message.body.output }) -join ''
    if ($output -notmatch 'packaged-dap=42') { throw 'Installed adapter did not deliver the expected program output.' }
    $null = Request 'disconnect' @{ terminateDebuggee = $true }
    # Leave stdin open: a successful disconnect must end the owned adapter naturally.
    if (-not $process.WaitForExit([Math]::Min(5000, (Get-RemainingMilliseconds)))) { throw 'Installed adapter did not exit after disconnect.' }
    if ($process.ExitCode -ne 0) { throw "Installed adapter exited with code $($process.ExitCode)." }
    $result.status = 'passed'
    $result.exitCode = $process.ExitCode
    $result.output = $output
    $result.coverage = @('installed-shim', 'initialize', 'launch', 'import', 'source-breakpoint', 'stackTrace',
        'evaluate', 'continue', 'output', 'exited', 'terminated', 'disconnect-with-stdin-open')
}
catch {
    $result.error = $_.Exception.Message
    throw "$($result.error) Evidence: $resultsRoot"
}
finally {
    if ($started -and -not $process.HasExited) {
        try {
            $process.Kill($true)
            $result.cleanup = if ($process.WaitForExit(3000)) { 'Process tree terminated' }
                else { 'Process tree did not exit within the cleanup deadline' }
        }
        catch { $result.cleanup = "Process tree cleanup failed: $($_.Exception.Message)" }
    }
    $captureCancellation.Cancel()
    $stderr = if ($standardError -and $standardError.IsCompletedSuccessfully) { $standardError.GetAwaiter().GetResult() }
        else { '[stderr capture did not complete before cleanup]' }
    $stderr | Set-Content -LiteralPath (Join-Path $resultsRoot 'stderr.log') -Encoding utf8NoBOM
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $resultsRoot 'result.json') -Encoding utf8NoBOM
    $process.Dispose()
    $captureCancellation.Dispose()
}
[pscustomobject] $result
