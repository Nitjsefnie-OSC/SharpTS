using SharpTS.Execution;
using SharpTS.Parsing;
using SharpTS.Runtime;
using SharpTS.Runtime.Types;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.RuntimeTests;

public class SharpTSClassLookupCacheTests
{
    [Fact]
    public async Task ConcurrentLookupsPreserveOwnInheritedAndMissingResults()
    {
        var inheritedMethod = CreateFunction();
        var inheritedStatic = CreateFunction();
        var inheritedGetter = CreateFunction();
        var inheritedSetter = CreateFunction();
        var ownMethod = CreateFunction();
        var ownStatic = CreateFunction();
        var ownGetter = CreateFunction();
        var ownSetter = CreateFunction();
        var parent = new SharpTSClass("Parent", null,
            new() { ["inherited"] = inheritedMethod },
            new() { ["inherited"] = inheritedStatic }, [],
            new() { ["inherited"] = inheritedGetter },
            new() { ["inherited"] = inheritedSetter });
        var child = new SharpTSClass("Child", parent,
            new() { ["own"] = ownMethod }, new() { ["own"] = ownStatic }, [],
            new() { ["own"] = ownGetter }, new() { ["own"] = ownSetter });

        void AssertPositiveLookups()
        {
            Assert.Same(ownMethod, child.FindMethod("own"));
            Assert.Same(inheritedMethod, child.FindMethod("inherited"));
            Assert.Same(ownStatic, child.FindStaticMethod("own"));
            Assert.Same(inheritedStatic, child.FindStaticMethod("inherited"));
            Assert.Same(ownGetter, child.FindGetter("own"));
            Assert.Same(inheritedGetter, child.FindGetter("inherited"));
            Assert.Same(ownSetter, child.FindSetter("own"));
            Assert.Same(inheritedSetter, child.FindSetter("inherited"));
        }

        await RunWorkersAsync(8, (worker, start, token) =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(15), token));
            for (var iteration = 0; iteration < 128; iteration++)
            {
                token.ThrowIfCancellationRequested();
                AssertPositiveLookups();
                AssertMissingLookups(child, "shared-miss");
                var uniqueMiss = $"missing-{worker}-{iteration}";
                AssertMissingLookups(child, uniqueMiss);
                AssertMissingLookups(child, uniqueMiss);
            }
            AssertPositiveLookups();
        });
    }

    [Theory]
    [InlineData("set")]
    [InlineData("delete")]
    [InlineData("define")]
    public void StaticMethodMutationInvalidatesAWarmLookup(string operation)
    {
        var original = CreateFunction();
        var klass = new SharpTSClass("Mutable", null, [], new() { ["method"] = original }, []);
        Assert.Same(original, klass.FindStaticMethod("method"));
        Assert.Same(original, klass.FindStaticMethod("method"));

        switch (operation)
        {
            case "set":
                klass.SetStaticProperty("method", 42d);
                Assert.Equal(42d, klass.GetStaticProperty("method"));
                break;
            case "delete":
                Assert.True(klass.DeleteStaticProperty("method"));
                Assert.Null(klass.GetOwnStaticPropertyDescriptor("method"));
                break;
            case "define":
                Assert.True(klass.DefineStaticProperty("method",
                    new SharpTSPropertyDescriptor(42d, writable: true, enumerable: true, configurable: true)));
                var descriptor = Assert.IsType<SharpTSPropertyDescriptor>(klass.GetOwnStaticPropertyDescriptor("method"));
                Assert.True(descriptor.HasValue);
                Assert.Equal(42d, descriptor.Value);
                Assert.True(descriptor.Writable);
                Assert.True(descriptor.Configurable);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        // Declaration lookup does not return a replacement data property.
        Assert.Null(klass.FindStaticMethod("method"));
        Assert.Null(klass.FindStaticMethod("method"));
    }

    [Fact]
    public async Task ConcurrentInterpretersKeepPromiseSubclassLookupsIsolated()
    {
        var prefix = Guid.NewGuid().ToString("N");
        await RunWorkersAsync(4, (worker, start, token) =>
        {
            var source = PromiseSource(worker, prefix);
            var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
            var typeMap = new TypeChecker().Check(statements);
            using var output = new StringWriter();
            using var errors = new StringWriter();
            using var interpreter = new Interpreter(stdout: output, stderr: errors);
            interpreter.SetVmTimeoutToken(token);
            using var registration = token.Register(interpreter.Shutdown);
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(15), token));

            interpreter.Interpret(statements, typeMap);

            Assert.Null(interpreter.LastUncaughtError);
            Assert.Equal("", errors.ToString());
            Assert.Equal($"{worker} true true own-{worker} {worker + 10} 128 64 64 {worker}\n",
                output.ToString().Replace("\r\n", "\n"));
        });
    }

    private static SharpTSFunction CreateFunction()
    {
        var statements = new Parser(new Lexer("function unused() {}").ScanTokens()).ParseOrThrow();
        return new SharpTSFunction(Assert.IsType<Stmt.Function>(Assert.Single(statements)), new RuntimeEnvironment());
    }

    private static void AssertMissingLookups(SharpTSClass klass, string name)
    {
        Assert.Null(klass.FindMethod(name));
        Assert.Null(klass.FindStaticMethod(name));
        Assert.Null(klass.FindGetter(name));
        Assert.Null(klass.FindSetter(name));
    }

    private static async Task RunWorkersAsync(int count, Action<int, Barrier, CancellationToken> work)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var start = new Barrier(count);
        var workers = Enumerable.Range(0, count).Select(worker => Task.Factory.StartNew(
            () => work(worker, start, cancellation.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default)).ToArray();
        var completed = Task.WhenAll(workers);
        try
        {
            await completed.WaitAsync(TimeSpan.FromSeconds(35));
        }
        finally
        {
            await cancellation.CancelAsync();
            try
            {
                await completed.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception) when (completed.IsCompleted)
            {
                // The original await reports worker failures after every worker has stopped.
            }
        }
    }

    private static string PromiseSource(int worker, string prefix) => $$"""
        class WorkerPromise extends Promise<number> {
            get label(): string { return "own-{{worker}}"; }
            read(): number { return {{worker + 10}}; }
        }
        async function exercise(): Promise<void> {
            const promise: any = new WorkerPromise(resolve => resolve({{worker}}));
            const constructor: any = WorkerPromise;
            let missing = 0;
            let staticMissing = 0;
            let writes = 0;
            for (let index = 0; index < 64; index++) {
                const name = "read_{{prefix}}_{{worker}}_" + index;
                if (promise[name] === undefined) missing++;
                if (promise[name] === undefined) missing++;
                if (constructor[name] === undefined) staticMissing++;
                promise.write_{{prefix}}_{{worker}} = index;
                if (promise.write_{{prefix}}_{{worker}} === index) writes++;
            }
            console.log({{worker}}, promise instanceof WorkerPromise, promise instanceof Promise,
                promise.label, promise.read(), missing, staticMissing, writes, await promise);
        }
        exercise();
        """;
}
