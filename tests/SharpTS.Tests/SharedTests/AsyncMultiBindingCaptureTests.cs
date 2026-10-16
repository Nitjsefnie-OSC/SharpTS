using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncMultiBindingCaptureTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["for-await", """
            async function run() {
                let calls = 0, iterations = 0;
                function rows() { calls++; return [[], [1], [2, 3]]; }
                for await (const row of rows()) iterations++;
                console.log(iterations, calls);
            }
            run().then(() => console.log("resolved"),
                error => console.log("rejected", String(error)));
            """, "3 1\nresolved\n"];
        yield return ["shared-live-bindings", """
            async function run() {
                let first = 1, second = 10;
                function add() { first++; second += 2; }
                function read() { console.log(first, second); }
                add();
                await Promise.resolve(0);
                read();
                first += 3;
                second += 4;
                add();
                read();
            }
            run();
            """, "2 12\n6 18\n"];
        yield return ["descendant-mutation", """
            async function run() {
                let value = 0, spare = 0;
                function update() { const before = value; (() => value++)(); return before; }
                function read() { return value; }
                update();
                await Promise.resolve(0);
                console.log(read());
                value = 5;
                update();
                console.log(read(), spare);
            }
            run();
            """, "1\n6 0\n"];
        yield return ["sync-control", """
            function run() {
                let calls = 0, iterations = 0;
                function rows() { calls++; return [[], [1], [2, 3]]; }
                for (const row of rows()) iterations++;
                console.log(iterations, calls);
            }
            run();
            """, "3 1\n"];
        yield return ["global-control", """
            let calls = 0;
            async function run() {
                let iterations = 0;
                function rows() { calls++; return [[], [1], [2, 3]]; }
                for await (const row of rows()) iterations++;
                console.log(iterations, calls);
            }
            run().then(() => console.log("resolved"));
            """, "3 1\nresolved\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void NestedFunctionsShareAllAsyncDeclarationBindings(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
