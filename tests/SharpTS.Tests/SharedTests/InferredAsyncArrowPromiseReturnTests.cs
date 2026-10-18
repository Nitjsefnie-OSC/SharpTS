using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InferredAsyncArrowPromiseReturnTests
{
    public const string ExactOriginal = """
        function produce() {
            const arrow = async () => { await Promise.resolve(0); };
            const task = arrow();
            return task;
        }
        const result: any = produce();
        console.log(result === undefined, typeof result);
        """;

    public const string CompatibleOuterReturns = """
        let release: any;
        const gate = new Promise<number>(resolve => { release = resolve; });
        let returned: any;
        function indirect(): Promise<void> {
            const arrow = async () => { await gate; };
            const task = arrow();
            returned = task;
            return task;
        }
        const directArrow = async () => { await gate; };
        function direct(): Promise<void> { return directArrow(); }
        const first = indirect(), second = direct();
        console.log(first === returned, typeof first, typeof second);
        release(0);
        async function observe(): Promise<void> {
            console.log((await first) === undefined, (await second) === undefined);
        }
        observe();
        """;

    public const string VoidBoundaryControls = """
        function nothing(): void {}
        const empty = async () => {};
        const bare = async () => { return; };
        const returnedVoid = async () => { return nothing(); };
        const explicit = async (): Promise<void> => {};
        const contextual: () => Promise<void> = async () => {};
        const expression = async () => nothing();
        const functionExpression = async function() {};
        function forwardEmpty() { return empty(); }
        function forwardBare() { return bare(); }
        function forwardReturnedVoid() { return returnedVoid(); }
        function forwardExplicit() { return explicit(); }
        function forwardContextual() { return contextual(); }
        function forwardExpression() { return expression(); }
        function forwardFunctionExpression() { return functionExpression(); }
        const a: any = forwardEmpty(), b: any = forwardBare(), c: any = forwardReturnedVoid();
        const d: any = forwardExplicit(), e: any = forwardContextual();
        const f: any = forwardExpression(), g: any = forwardFunctionExpression();
        console.log(typeof a, typeof b, typeof c, typeof d, typeof e, typeof f, typeof g);
        async function observe(): Promise<void> {
            console.log((await a) === undefined, (await b) === undefined, (await c) === undefined,
                (await d) === undefined, (await e) === undefined, (await f) === undefined, (await g) === undefined);
        }
        observe();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-original", ExactOriginal, "false object\n"];
        yield return ["compatible-outer-returns", CompatibleOuterReturns, "true object object\ntrue true\n"];
        yield return ["void-boundary-controls", VoidBoundaryControls, "object object object object object object object\ntrue true true true true true true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InferredAsyncArrowsPreservePromiseReturns(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
