using SharpTS.Execution;
using SharpTS.Execution.Debugging;
using SharpTS.Modules;
using SharpTS.Parsing;
using SharpTS.Runtime.Types;
using Xunit;

namespace SharpTS.Tests.DebugAdapter;

[Collection("DebugAdapterTests")]
public sealed class InterpreterDebugHostTests
{
    [Fact]
    public void NonDebugWorkerDoesNotAllocateAControllerOrHost()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), "sharpts-worker-debug-audit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string script = Path.Combine(directory, "idle-worker.ts");
            File.WriteAllText(script, "setInterval(() => {}, 1000);");
            using var parent = new Interpreter(TextWriter.Null, TextWriter.Null);
            using var worker = new SharpTSWorker(script, options: null, parentInterpreter: parent);
            var interpreterField = typeof(SharpTSWorker).GetField(
                "_workerInterpreter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

            Assert.True(SpinWait.SpinUntil(
                () => interpreterField.GetValue(worker) is Interpreter,
                TimeSpan.FromSeconds(5)));
            var workerInterpreter = Assert.IsType<Interpreter>(interpreterField.GetValue(worker));
            Assert.Null(workerInterpreter.DebugHost);
            Assert.Null(workerInterpreter.DebugController);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WorkerIdsAreMonotonicAndConfigurationIsInherited()
    {
        using var host = new InterpreterDebugHost();
        using var main = new Interpreter(TextWriter.Null, TextWriter.Null);
        host.ConfigureExceptionFilters(caught: true, uncaught: false, unhandledRejection: false);
        host.ConfigureJustMyCode(enabled: false);
        host.RegisterMain(main);

        int firstId;
        using (var first = new Interpreter(TextWriter.Null, TextWriter.Null))
        using (host.RegisterWorker(first, "first", static interpreter => interpreter.Shutdown()))
        {
            InterpreterDebugThreadInfo thread = Assert.Single(
                host.Threads, candidate => candidate.Id != 1);
            firstId = thread.Id;
            Assert.True(thread.Controller.BreakOnCaughtException);
            Assert.False(thread.Controller.BreakOnUncaughtException);
        }

        using var second = new Interpreter(TextWriter.Null, TextWriter.Null);
        using (host.RegisterWorker(second, "second", static interpreter => interpreter.Shutdown()))
        {
            InterpreterDebugThreadInfo thread = Assert.Single(
                host.Threads, candidate => candidate.Id != 1);
            Assert.True(thread.Id > firstId);
            Assert.NotEqual(firstId, thread.Id);
        }
    }

    [Fact]
    public void LateWorkerSourceRebindsStableBreakpointId()
    {
        using var host = new InterpreterDebugHost();
        using var main = new Interpreter(TextWriter.Null, TextWriter.Null);
        host.RegisterMain(main);
        ParsedModule module = ParseModule("C:/debug/late-worker.ts", "let workerValue = 42;");

        DebugBreakpointBinding pending = Assert.Single(host.SetBreakpoints(module.Path, [(1, 1)]));
        Assert.False(pending.Verified);
        DebugBreakpointBinding? changed = null;
        host.BreakpointChanged += binding => changed = binding;

        using var worker = new Interpreter(TextWriter.Null, TextWriter.Null);
        IDisposable registration = host.RegisterWorker(
            worker, "worker", static interpreter => interpreter.Shutdown());
        worker.DebugController!.RegisterModule(module);

        Assert.NotNull(changed);
        Assert.Equal(pending.Id, changed.Id);
        Assert.True(changed.Verified);
        Assert.Equal(1, changed.Line);
        int sourceId = Assert.Single(host.Sources).Id;

        changed = null;
        registration.Dispose();
        Assert.Empty(host.Sources);
        Assert.NotNull(changed);
        Assert.Equal(pending.Id, changed.Id);
        Assert.False(changed.Verified);
        Assert.True(sourceId > 0);
    }

    [Fact]
    public void WorkerReloadOfChangedPathKeepsEachSourceSnapshotAndInvalidatesAllBindings()
    {
        using var host = new InterpreterDebugHost();
        using var main = new Interpreter(TextWriter.Null, TextWriter.Null);
        InterpreterDebugThreadInfo mainThread = host.RegisterMain(main);
        const string path = "C:/debug/shared.ts";
        ParsedModule original = ParseModule(path, "let value = 1;");
        mainThread.Controller.RegisterModule(original);
        DebugBreakpointBinding binding = Assert.Single(host.SetBreakpoints(path, [(1, 1)]));
        Assert.True(binding.Verified);
        InterpreterDebugSourceInfo originalSource = host.GetSource(original.Document!);
        var changed = new List<DebugBreakpointBinding>();
        host.BreakpointChanged += changed.Add;

        using var worker = new Interpreter(TextWriter.Null, TextWriter.Null);
        using IDisposable registration = host.RegisterWorker(
            worker, "worker", static interpreter => interpreter.Shutdown());
        ParsedModule replacement = ParseModule(path, "// changed\nlet value = 2;");
        worker.DebugController!.RegisterModule(replacement);

        InterpreterDebugSourceInfo replacementSource = host.GetSource(replacement.Document!);
        Assert.NotEqual(originalSource.Id, replacementSource.Id);
        Assert.True(host.TryGetSource(originalSource.Id, out InterpreterDebugSourceInfo? first));
        Assert.True(host.TryGetSource(replacementSource.Id, out InterpreterDebugSourceInfo? second));
        Assert.Equal(original.Document!.Text, first!.Document.Text);
        Assert.Equal(replacement.Document!.Text, second!.Document.Text);
        Assert.Equal(2, host.Sources.Count);
        Assert.True(host.HasChangedSource(original.Document));
        Assert.True(host.HasChangedSource(replacement.Document));
        Assert.Contains(changed, candidate => candidate.Id == binding.Id && !candidate.Verified);
        Assert.False(Assert.Single(host.SetBreakpoints(path, [(1, 1)])).Verified);
    }

    [Fact]
    public void ExitedWorkerSourceVersionsAreReleasedWhileMainKeepsSharedPathLoaded()
    {
        using var host = new InterpreterDebugHost();
        using var main = new Interpreter(TextWriter.Null, TextWriter.Null);
        InterpreterDebugThreadInfo mainThread = host.RegisterMain(main);
        const string path = "C:/debug/shared-snapshot-lifetime.ts";
        ParsedModule original = ParseModule(path, "let value = 1;");
        mainThread.Controller.RegisterModule(original);
        int originalSourceId = host.GetSource(original.Document!).Id;
        var removed = new List<int>();
        host.SourceChanged += change =>
        {
            if (change.Reason == InterpreterDebugSourceChangeReason.Removed)
                removed.Add(change.Source.Id);
        };

        for (int index = 0; index < 3; index++)
        {
            using var worker = new Interpreter(TextWriter.Null, TextWriter.Null);
            IDisposable registration = host.RegisterWorker(
                worker, "worker", static interpreter => interpreter.Shutdown());
            ParsedModule replacement = ParseModule(path, $"let value = {index + 2};");
            worker.DebugController!.RegisterModule(replacement);
            int workerSourceId = host.GetSource(replacement.Document!).Id;
            Assert.Equal(2, host.Sources.Count);

            registration.Dispose();

            Assert.False(host.TryGetSource(workerSourceId, out _));
            Assert.Contains(workerSourceId, removed);
            Assert.Equal(originalSourceId, Assert.Single(host.Sources).Id);
            Assert.True(host.TryGetSource(originalSourceId, out _));
        }
    }

    [Fact]
    public void PhysicalSourceDeletedAfterCaptureBeforeRegistrationCannotVerifyBreakpoints()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), "sharpts-dap-capture-deletion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "main.ts");
            File.WriteAllText(path, "let value = 1;");
            SourceDocument document = SourceDocument.FromBytes(path, File.ReadAllBytes(path));
            File.Delete(path);
            var result = new Parser(new Lexer(document.Text).ScanTokens())
                .WithSourceDocument(document).Parse();
            Assert.True(result.IsSuccess);
            var module = new ParsedModule(path, result.Statements) { Document = document };
            using var host = new InterpreterDebugHost();
            using var main = new Interpreter(TextWriter.Null, TextWriter.Null);
            InterpreterDebugThreadInfo mainThread = host.RegisterMain(main);
            mainThread.Controller.RegisterModule(module);

            Assert.False(Assert.Single(mainThread.Controller.SetBreakpoints(path, [(1, 1)])).Verified);
            Assert.False(Assert.Single(host.SetBreakpoints(path, [(1, 1)])).Verified);
            Assert.True(host.HasChangedSource(document));
            Assert.Equal(document.Text, host.GetSource(document).Document.Text);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CoordinatedPauseReportsEachParkedThreadAndResumeReleasesAll()
    {
        using var host = new InterpreterDebugHost();
        using var main = new Interpreter(TextWriter.Null, TextWriter.Null);
        using var worker = new Interpreter(TextWriter.Null, TextWriter.Null);
        InterpreterDebugThreadInfo mainThread = host.RegisterMain(main);
        using IDisposable registration = host.RegisterWorker(
            worker, "worker", static interpreter => interpreter.Shutdown());
        InterpreterDebugThreadInfo workerThread = host.Threads.Single(thread => thread.Id != 1);
        ParsedModule mainModule = ParseModule("C:/debug/main.ts", "let mainValue = 1;");
        ParsedModule workerModule = ParseModule("C:/debug/worker.ts", "let workerValue = 2;");
        mainThread.Controller.RegisterModule(mainModule);
        workerThread.Controller.RegisterModule(workerModule);
        host.StartMain(stopOnEntry: false);

        var stopped = new List<InterpreterDebugStopEvent>();
        var allStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Stopped += stop =>
        {
            lock (stopped)
                stopped.Add(stop);
            if (stop.AllThreadsStopped)
                allStopped.TrySetResult();
        };

        host.RequestPause(mainThread.Id);
        Task mainPark = Task.Run(() => mainThread.Controller.OnSafePoint(
            main, mainModule.Statements[0], main.Environment, mainModule));
        Task workerPark = Task.Run(() => workerThread.Controller.OnSafePoint(
            worker, workerModule.Statements[0], worker.Environment, workerModule));
        await allStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));

        lock (stopped)
        {
            Assert.Contains(stopped, stop => stop.ThreadId == mainThread.Id);
            Assert.Contains(stopped, stop => stop.ThreadId == workerThread.Id);
            Assert.True(stopped[^1].AllThreadsStopped);
            Assert.Single(stopped.Select(stop => stop.Epoch).Distinct());
        }

        host.Continue(workerThread.Id, DebugStepKind.Over);
        await Task.WhenAll(mainPark, workerPark).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(host.Threads, thread => Assert.Null(thread.CurrentStop));
    }

    [Fact]
    public async Task TerminationWakesEveryControllerAndWaitsForWorkerUnregistration()
    {
        using var host = new InterpreterDebugHost();
        using var main = new Interpreter(TextWriter.Null, TextWriter.Null);
        using var worker = new Interpreter(TextWriter.Null, TextWriter.Null);
        InterpreterDebugThreadInfo mainThread = host.RegisterMain(main);
        var workerShutdown = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable registration = host.RegisterWorker(worker, "worker", interpreter =>
        {
            interpreter.Shutdown();
            workerShutdown.TrySetResult();
        });
        host.StartMain(stopOnEntry: false);

        Task termination = host.Terminate();
        await workerShutdown.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(termination.IsCompleted);
        Assert.Equal(DebugExecutionState.Terminating, mainThread.Controller.State);

        registration.Dispose();
        await termination.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(host.Threads, thread => thread.Id != 1);
    }

    [Fact]
    public async Task WorkerExitCanCompleteAnInProgressStopConvergence()
    {
        using var host = new InterpreterDebugHost();
        using var main = new Interpreter(TextWriter.Null, TextWriter.Null);
        using var parkedWorker = new Interpreter(TextWriter.Null, TextWriter.Null);
        using var exitingWorker = new Interpreter(TextWriter.Null, TextWriter.Null);
        InterpreterDebugThreadInfo mainThread = host.RegisterMain(main);
        using IDisposable parkedRegistration = host.RegisterWorker(
            parkedWorker, "parked", static interpreter => interpreter.Shutdown());
        IDisposable exitingRegistration = host.RegisterWorker(
            exitingWorker, "exiting", static interpreter => interpreter.Shutdown());
        InterpreterDebugThreadInfo parkedThread = host.Threads.Single(thread =>
            ReferenceEquals(thread.Interpreter, parkedWorker));
        ParsedModule mainModule = ParseModule("C:/debug/convergence-main.ts", "let mainValue = 1;");
        ParsedModule workerModule = ParseModule("C:/debug/convergence-worker.ts", "let workerValue = 2;");
        mainThread.Controller.RegisterModule(mainModule);
        parkedThread.Controller.RegisterModule(workerModule);
        host.StartMain(stopOnEntry: false);

        int parkedCount = 0;
        var twoParked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Stopped += stop =>
        {
            if (Interlocked.Increment(ref parkedCount) == 2)
                twoParked.TrySetResult();
            if (stop.AllThreadsStopped)
                allStopped.TrySetResult();
        };

        host.RequestPause(mainThread.Id);
        Task mainPark = Task.Run(() => mainThread.Controller.OnSafePoint(
            main, mainModule.Statements[0], main.Environment, mainModule));
        Task workerPark = Task.Run(() => parkedThread.Controller.OnSafePoint(
            parkedWorker, workerModule.Statements[0], parkedWorker.Environment, workerModule));
        await twoParked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(allStopped.Task.IsCompleted);

        exitingRegistration.Dispose();
        await allStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        host.Continue(mainThread.Id);
        await Task.WhenAll(mainPark, workerPark).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static ParsedModule ParseModule(string path, string source)
    {
        var document = new SourceDocument(path, source);
        var parser = new Parser(new Lexer(source).ScanTokens()).WithSourceDocument(document);
        var result = parser.Parse();
        Assert.True(result.IsSuccess);
        return new ParsedModule(path, result.Statements) { Document = document };
    }
}
