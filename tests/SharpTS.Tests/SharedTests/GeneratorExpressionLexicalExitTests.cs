using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GeneratorExpressionLexicalExitTests
{
    public const string AsyncTryReturnSource = """
        function pause(): Promise<void> {
            return new Promise<void>(resolve => { setTimeout(() => resolve(), 1); });
        }
        let read: any;
        async function compute(): Promise<number> {
            try {
                let value = 1;
                read = function*(): Generator<number, void, unknown> { yield value; };
                await pause();
                value += 4;
                return value;
            } finally {
                console.log("finally", read().next().value);
            }
        }
        async function run(): Promise<void> {
            console.log(await compute(), read().next().value);
        }
        run();
        """;

    public const string AsyncCatchRethrowSource = """
        function pause(): Promise<void> {
            return new Promise<void>(resolve => { setTimeout(() => resolve(), 1); });
        }
        function rejectLater(reason: string): Promise<void> {
            return new Promise<void>((resolve, reject) => { setTimeout(() => reject(reason), 1); });
        }
        let read: any;
        async function compute(): Promise<void> {
            try {
                await rejectLater("first");
            } catch (reason: any) {
                let value = 2;
                read = function*(): Generator<string, void, unknown> { yield reason + ":" + value; };
                await pause();
                reason = "changed";
                value += 3;
                throw "second";
            } finally {
                console.log("finally", read().next().value);
            }
        }
        async function run(): Promise<void> {
            try { await compute(); }
            catch (reason: any) { console.log("caught", reason, read().next().value); }
        }
        run();
        """;

    public const string AsyncFinallyPendingThrowSource = """
        function pause(): Promise<void> {
            return new Promise<void>(resolve => { setTimeout(() => resolve(), 1); });
        }
        let read: any;
        async function compute(): Promise<void> {
            try {
                await pause();
                throw "original";
            } finally {
                let value = 10;
                read = async function*(): AsyncGenerator<number, void, unknown> { yield value; };
                const current = () => value;
                await pause();
                value += 7;
                console.log("cleanup", current(), (await read().next()).value);
            }
        }
        async function run(): Promise<void> {
            try { await compute(); }
            catch (reason: any) { console.log("caught", reason, (await read().next()).value); }
        }
        run();
        """;

    public const string SyncGeneratorOwnerSource = """
        let read: any;
        function* outer(): Generator<string, void, unknown> {
            try {
                let value = 1;
                read = function*(): Generator<number, void, unknown> { yield value; };
                yield "ready";
                value += 2;
                throw "boom";
            } catch (reason: any) {
                let value = 10;
                const readCatch = function*(): Generator<string, void, unknown> { yield reason + ":" + value; };
                yield "caught";
                value += 1;
                console.log("catch", readCatch().next().value, read().next().value);
            } finally {
                let value = 20;
                const readFinally = function*(): Generator<number, void, unknown> { yield value; };
                yield "cleanup";
                value += 1;
                console.log("final", readFinally().next().value);
            }
        }
        const iterator = outer();
        console.log(iterator.next().value, read().next().value);
        console.log(iterator.next().value, read().next().value);
        console.log(iterator.next().value);
        console.log(iterator.next().done, read().next().value);
        """;

    public const string AsyncGeneratorOwnerSource = """
        function pause(): Promise<void> {
            return new Promise<void>(resolve => { setTimeout(() => resolve(), 1); });
        }
        let read: any;
        async function* outer(): AsyncGenerator<string, void, unknown> {
            try {
                let value = 1;
                read = function*(): Generator<number, void, unknown> { yield value; };
                await pause();
                yield "ready";
                value += 2;
                throw "boom";
            } catch (reason: any) {
                let value = 10;
                const readCatch = async function*(): AsyncGenerator<string, void, unknown> { yield reason + ":" + value; };
                await pause();
                value += 1;
                console.log("catch", (await readCatch().next()).value, read().next().value);
                yield "caught";
            } finally {
                let value = 20;
                const readFinally = function*(): Generator<number, void, unknown> { yield value; };
                await pause();
                value += 1;
                console.log("final", readFinally().next().value);
            }
        }
        async function run(): Promise<void> {
            const iterator = outer();
            console.log((await iterator.next()).value, read().next().value);
            console.log((await iterator.next()).value, read().next().value);
            console.log((await iterator.next()).done, read().next().value);
        }
        run();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["async-try-return", AsyncTryReturnSource, "finally 5\n5 5\n"];
        yield return ["async-catch-rethrow", AsyncCatchRethrowSource, "finally changed:5\ncaught second changed:5\n"];
        yield return ["async-finally-pending-throw", AsyncFinallyPendingThrowSource, "cleanup 17 17\ncaught original 17\n"];
        yield return ["sync-generator-owner", SyncGeneratorOwnerSource, "ready 1\ncaught 3\ncatch boom:11 3\ncleanup\nfinal 21\ntrue 3\n"];
        yield return ["async-generator-owner", AsyncGeneratorOwnerSource, "ready 1\ncatch boom:11 3\ncaught 3\nfinal 21\ntrue 3\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void CapturedLexicalCellsSurviveSuspensionAndProtectedRegionExits(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
