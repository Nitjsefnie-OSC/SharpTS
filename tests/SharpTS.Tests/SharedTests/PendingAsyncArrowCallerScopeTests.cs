using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PendingAsyncArrowCallerScopeTests
{
    // The per-flow interpreter frame ownership repair (#2155) also fixes #2263.
    // The synchronous wrapper has already returned when this pending arrow resumes.
    public const string IssueSource = """
        class C {
            start(pending: Promise<number>) {
                return (async () => await pending)();
            }
        }
        async function run() {
            let release: (value: number) => void = () => {};
            const pending = new Promise<number>(resolve => { release = resolve; });
            const marker = { value: 7 };
            const task = new C().start(pending);
            release(2);
            console.log(await task, marker.value);
        }
        run().catch((error: any) => console.log(error.name, error.message));
        """;

    private const string RejectionSource = """
        class C {
            start(pending: Promise<number>) {
                return (async () => await pending)();
            }
        }
        async function run() {
            let fail: (reason: any) => void = () => {};
            const pending = new Promise<number>((resolve, reject) => { fail = reject; });
            const marker = { value: 7 };
            const task = new C().start(pending);
            fail(new Error("boom"));
            try { await task; }
            catch (error: any) { console.log(error.message, marker.value); }
            console.log("after", marker.value);
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-expression-resolve", IssueSource, "2 7\n"];
        yield return ["returned-arrow-control", IssueSource
            .Replace("return (async () => await pending)();", "return async () => await pending;", StringComparison.Ordinal)
            .Replace("const task = new C().start(pending);",
                "const operation = new C().start(pending);\n    const task = operation();", StringComparison.Ordinal),
            "2 7\n"];
        yield return ["block-resolve", WithBody(IssueSource, "{ return await pending; }"), "2 7\n"];
        yield return ["expression-reject", RejectionSource, "boom 7\nafter 7\n"];
        yield return ["block-reject", WithBody(RejectionSource, "{ return await pending; }"),
            "boom 7\nafter 7\n"];

        var lexicalThisSource = IssueSource.Replace("class C {", "class C {\n    value = 11;", StringComparison.Ordinal);
        yield return ["expression-lexical-this", WithBody(lexicalThisSource, "(await pending) + this.value"),
            "13 7\n"];
        yield return ["block-lexical-this", WithBody(lexicalThisSource,
            "{ const result = await pending; return result + this.value; }"), "13 7\n"];
    }

    private static string WithBody(string source, string body) =>
        source.Replace("async () => await pending", "async () => " + body, StringComparison.Ordinal);

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void PendingArrowPreservesCallerScopeAfterSynchronousMethodReturns(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
