using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class HandlerLexicalScopeTests
{
    public const string Exact = """
        function make(): any {
            let read: any;
            try {
                let value = 1;
                read = () => value;
            } finally {
                let value = 2;
                console.log("finally", value);
            }
            return read;
        }
        console.log("retained", make()());
        """;

    public const string RetainedShadowCatch = """
        const token: any = {};
        function make(seed: number): any {
            let value = 1000;
            let readTry: any, readCatch: any, readFinally: any;
            try {
                let value = seed;
                readTry = () => value;
                throw token;
            } catch (reason: any) {
                let value = seed + 10;
                readCatch = () => (reason === token) + ":" + value;
            } finally {
                let value = seed + 20;
                readFinally = () => value;
            }
            return { readTry, readCatch, readFinally, readOuter: () => value };
        }
        const first = make(1), second = make(3);
        console.log(first.readTry(), first.readCatch(), first.readFinally(), first.readOuter());
        console.log(second.readTry(), second.readCatch(), second.readFinally(), second.readOuter());
        """;

    public const string AbruptControl = """
        const token: any = {}, other: any = {};
        function fromTry(): any {
            try { let value = 1; return () => value; }
            finally { let value = 2; console.log("cleanup", value); }
        }
        function fromCatch(): any {
            try { throw token; }
            catch (reason: any) { let value = 3; return () => (reason === token) + ":" + value; }
            finally { let value = 4; console.log("cleanup", value); }
        }
        function overrideReturn(): number { try { return 1; } finally { return 2; } }
        function overrideThrow(): void { try { throw token; } finally { throw other; } }
        console.log(fromTry()());
        console.log(fromCatch()());
        console.log(overrideReturn());
        try { overrideThrow(); } catch (error) { console.log(error === other); }
        """;

    public const string UncapturedHandlerShadows = """
        function run(): void {
            let value = 1;
            const read = () => value;
            try {
                let value = 10;
                value += 1;
                console.log("try", value);
                throw 20;
            } catch (value: any) {
                value += 2;
                console.log("catch", value);
            } finally {
                let value = 30;
                value += 3;
                console.log("finally", value);
            }
            console.log("outer", read(), value);
        }
        run();
        """;

    public const string RepeatedHandlerEntries = """
        function collect(): any[] {
            const entries: any[] = [];
            for (const seed of [1, 10]) {
                try {
                    let value = seed;
                    const read = () => value;
                    const add = (step: number) => { value += step; return value; };
                    entries.push({ read, add });
                } finally {
                    console.log("cleanup", seed);
                }
            }
            return entries;
        }
        const entries = collect();
        console.log(entries[0].read(), entries[1].read());
        console.log(entries[0].add(2), entries[1].read(), entries[0].read());
        console.log(entries[1].add(3), entries[0].read());
        """;

    public const string HandlerVarVisibility = """
        class Box {
            get read(): any {
                try { var value = 1; } finally {}
                return () => value;
            }
            get mixed(): any {
                try { var value = 1; let inner = 2; value += inner; } finally {}
                return () => value;
            }
            get nested(): number {
                try {
                    let outer = 2;
                    try { var value = 1; let inner = 3; value += inner; } finally {}
                    value += outer;
                } finally {}
                return value;
            }
        }
        function make(): any {
            try { var value = 1; let inner = 2; value += inner; } finally {}
            return () => value;
        }
        const box = new Box();
        console.log(box.read(), box.mixed(), box.nested, make()());
        const read = () => typeof Number;
        namespace Hidden { var Number = 1; }
        console.log(read());
        """;

    public const string ScriptRootVar = """
        var readHandlerRoot: any;
        try {
            var handlerTryValue = 7;
            readHandlerRoot = () => handlerTryValue;
        }
        finally { var handlerFinallyValue = 8; }
        handlerTryValue = 9;
        console.log(readHandlerRoot(), handlerTryValue, handlerFinallyValue);
        class HandlerVarGetter {
            get value(): number {
                var value = 1;
                try { var value = 2; }
                finally { value += 3; }
                return value;
            }
        }
        console.log(new HandlerVarGetter().value);
        """;

    public const string VarPlacementControl = """
        function sync(): void {
            try { var left = 1; let value = 10; console.log("try", value); }
            finally { var right = 2; let value = 20; console.log("finally", value); }
            console.log("vars", left, right);
        }
        async function asynchronous(): Promise<void> {
            try { var left = 3; let value = 30; await Promise.resolve(0); console.log("try", value); }
            finally { var right = 4; let value = 40; await Promise.resolve(0); console.log("finally", value); }
            console.log("vars", left, right);
        }
        sync();
        asynchronous().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string NestedVarCallBoundary = """
        function sync(): void {
            var shared = 1;
            var read = () => shared;
            try {
                var first = 2;
                try { var nested = 3; }
                finally { var cleanup = 4; }
                function local(): number { var shared = 90; return shared; }
                console.log("sync-call", local(), shared);
            } finally { var last = 5; shared = 6; }
            console.log("sync-vars", first, nested, cleanup, last, read());
        }
        async function asynchronous(wait: Promise<any>): Promise<void> {
            var shared = 10;
            var read = () => shared;
            try {
                var first = 11;
                try { var nested = 12; await wait; }
                finally { var cleanup = 13; }
                function local(): number { var shared = 90; return shared; }
                console.log("async-call", local(), shared);
            } finally { var last = 14; shared = 15; }
            console.log("async-vars", first, nested, cleanup, last, read());
        }
        sync();
        var release: any;
        var gate = new Promise<any>(resolve => { release = resolve; });
        var pending = asynchronous(gate);
        console.log("pending");
        release(0);
        pending.catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string OuterBindingRestoration = """
        const token: any = {};
        function make(seed: number): any {
            let outer = seed + 1;
            let read: any = () => -1;
            var initialized = 0;
            try {
                var initialized = outer + 1;
                let local = initialized + 1;
                read = () => seed + outer + local;
                throw token;
            } catch (error: any) {
                let caught = outer + initialized;
                return () => (error === token) + ":" + seed + ":" + outer + ":" + initialized + ":" + caught + ":" + read();
            } finally {
                let cleanup = outer + 10;
                console.log("cleanup", cleanup, seed, outer);
                outer += 1;
            }
        }
        const first = make(1), second = make(3);
        console.log("retained", first(), second());
        """;

    private static IEnumerable<object[]> RetainedLexicalCases()
    {
        yield return ["exact-retained-try", Exact, "finally 2\nretained 1\n"];
        yield return ["retained-shadow-catch", RetainedShadowCatch, "1 true:11 21 1000\n3 true:13 23 1000\n"];
        yield return ["abrupt-handler-completion", AbruptControl, "cleanup 2\n1\ncleanup 4\ntrue:3\n2\ntrue\n"];
    }

    public static IEnumerable<object[]> LexicalCases()
    {
        foreach (var row in RetainedLexicalCases())
            yield return row;
        yield return ["uncaptured-handler-shadows", UncapturedHandlerShadows, "try 11\ncatch 22\nfinally 33\nouter 1 1\n"];
        yield return ["repeated-handler-entries", RepeatedHandlerEntries, "cleanup 1\ncleanup 10\n1 10\n3 10 3\n13 3\n"];
        yield return ["handler-var-visibility", HandlerVarVisibility, "1 3 6 3\nfunction\n"];
    }

    public static IEnumerable<object[]> InterpreterOnlyCases()
    {
        yield return ["script-root-var", ScriptRootVar, "9 9 8\n5\n"];
    }

    public static IEnumerable<object[]> InterpreterCases() => RetainedLexicalCases()
        .Concat(InterpreterOnlyCases());

    public static IEnumerable<object[]> VarCases()
    {
        yield return ["var-placement-preservation", VarPlacementControl,
            "try 10\nfinally 20\nvars 1 2\ntry 30\nfinally 40\nvars 3 4\n"];
        yield return ["nested-var-call-boundary", NestedVarCallBoundary,
            "sync-call 90 1\nsync-vars 2 3 4 5 6\npending\nasync-call 90 10\nasync-vars 11 12 13 14 15\n"];
    }

    public static IEnumerable<object[]> ResolverCases() => InterpreterCases()
        .Concat(VarCases())
        .Append(["outer-binding-restoration", OuterBindingRestoration,
            "cleanup 12 1 2\ncleanup 14 3 4\nretained true:1:3:3:5:8 true:3:5:5:9:14\n"]);

    public static IEnumerable<object[]> VarModeCases() => VarCases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    public static IEnumerable<object[]> LexicalModeCases() => LexicalCases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(LexicalModeCases))]
    public void HandlerLexicalBindingsKeepIndependentStorage(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(InterpreterOnlyCases))]
    public void InterpreterPreservesHandlerLexicalEnvironments(string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunInterpreted(source));
    }

    [Theory, MemberData(nameof(VarModeCases))]
    public void HandlerScopesPreserveVarPlacement(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
