using System.Diagnostics;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public async Task Isolated_ProcessCapture_PreservesUtf8BomAcrossSplitReads()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        using var process = Process.Start(CaptureFixtureStart(directory.Path, "utf8-output"))!;
        using var cancellation = new CancellationTokenSource();
        var clock = Stopwatch.StartNew();
        // Force every UTF-8/BOM sequence across native read boundaries on Linux.
        // Other platforms exercise their unchanged async capture/decoder on the same child.
        var stdout = new StandalonePipeCapture(process.StandardOutput, clock, cancellation.Token,
            linuxReadChunkBytes: 1);
        var stderr = new StandalonePipeCapture(process.StandardError, clock, cancellation.Token);
        try
        {
            Assert.True(process.WaitForExit(10000));
            Assert.Equal(0, process.ExitCode);
            Assert.True(stdout.WaitForStop(5000));
            Assert.True(stderr.WaitForStop(5000));
            Assert.Equal("Aé😀中|tail", await stdout.Completion);
            Assert.Equal("utf8-stderr", await stderr.Completion);
            Assert.NotNull(stdout.Snapshot().Eof);
            Assert.Null(stdout.Snapshot().Canceled);
            Assert.Null(stdout.Snapshot().Fault);
        }
        finally
        {
            await cancellation.CancelAsync();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            Assert.True(process.WaitForExit(5000));
            Assert.True(stdout.WaitForStop(5000));
            Assert.True(stderr.WaitForStop(5000));
        }
    }

    [Fact]
    public async Task Isolated_ProcessCapture_CancellationCompletesWithoutEof()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        using var process = Process.Start(CaptureFixtureStart(directory.Path, "child"))!;
        using var cancellation = new CancellationTokenSource();
        var clock = Stopwatch.StartNew();
        var stdout = new StandalonePipeCapture(process.StandardOutput, clock, cancellation.Token);
        var stderr = new StandalonePipeCapture(process.StandardError, clock, cancellation.Token);
        try
        {
            Assert.True(SpinWait.SpinUntil(() => stdout.Snapshot().ReadStarted is not null &&
                stderr.Snapshot().ReadStarted is not null, TimeSpan.FromSeconds(5)));
            Assert.False(process.HasExited);
            await cancellation.CancelAsync();
            foreach (var capture in new[] { stdout, stderr })
            {
                Assert.True(capture.WaitForStop(5000));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.Completion);
                Assert.NotNull(capture.Snapshot().Canceled);
                Assert.Null(capture.Snapshot().Eof);
                Assert.Null(capture.Snapshot().Fault);
                Assert.Equal(0, capture.Snapshot().CharacterCount);
            }
        }
        finally
        {
            await cancellation.CancelAsync();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            Assert.True(process.WaitForExit(5000));
            Assert.True(stdout.WaitForStop(5000));
            Assert.True(stderr.WaitForStop(5000));
        }
    }
}
