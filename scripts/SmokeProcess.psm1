Set-StrictMode -Version Latest

function Invoke-SmokeProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $FileName,
        [string[]] $Arguments = @(),
        [Parameter(Mandatory)] [string] $WorkingDirectory,
        [Parameter(Mandatory)] [string] $LogDirectory,
        [ValidateRange(1, 2147483647)] [int] $TimeoutMilliseconds = 180000
    )

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FileName
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $captureCancellation = [Threading.CancellationTokenSource]::new()
    $logPath = Join-Path $LogDirectory "$Name.log"
    $cleanup = 'Not required'
    $exitCode = $null
    $timedOut = $false
    $text = ''
    $errors = ''
    try {
        if (-not $process.Start()) { throw "Could not start $Name." }
        $stdout = $process.StandardOutput.ReadToEndAsync($captureCancellation.Token)
        $stderr = $process.StandardError.ReadToEndAsync($captureCancellation.Token)
        $exit = $process.WaitForExitAsync()
        $completion = [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]] @($stdout, $stderr, $exit))
        # The deadline includes pipe EOF: descendants can retain handles after the parent exits.
        $timedOut = -not $completion.Wait($TimeoutMilliseconds)
        if ($timedOut) {
            $cleanup = 'Parent already exited; inherited output pipes exceeded the deadline'
            if (-not $process.HasExited) {
                try {
                    $process.Kill($true)
                    $cleanup = if ($process.WaitForExit(3000)) { 'Process tree terminated' }
                        else { 'Process tree did not exit within the cleanup deadline' }
                }
                catch { $cleanup = "Process tree cleanup failed: $($_.Exception.Message)" }
            }
            $captureCancellation.Cancel()
        }
        if ($process.HasExited) { $exitCode = $process.ExitCode }
        $text = if ($stdout.IsCompletedSuccessfully) { $stdout.GetAwaiter().GetResult() }
            else { '[stdout capture did not complete before the deadline]' }
        $errors = if ($stderr.IsCompletedSuccessfully) { $stderr.GetAwaiter().GetResult() }
            else { '[stderr capture did not complete before the deadline]' }
        if ($timedOut) { throw "$Name exceeded $TimeoutMilliseconds milliseconds. Evidence: $logPath" }
        if ($exitCode -ne 0) {
            throw "$Name failed with exit code ${exitCode}. Evidence: $logPath`n$text$errors"
        }
        return $text.Trim()
    }
    finally {
        @(
            "Command: $FileName $($Arguments -join ' ')"
            "Working directory: $WorkingDirectory"
            "Exit code: $exitCode"
            "Timed out: $timedOut"
            "Cleanup: $cleanup"
            'Standard output:'
            $text
            'Standard error:'
            $errors
        ) | Set-Content -LiteralPath $logPath -Encoding utf8NoBOM
        $captureCancellation.Cancel()
        $process.Dispose()
        $captureCancellation.Dispose()
    }
}

Export-ModuleMember -Function Invoke-SmokeProcess
