using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassConstructorIteratorTests
{
    public const string SyncIssueSource = """
        class C { static *[Symbol.iterator]() { yield 42; } }
        const iterable: any = C;
        for (const value of iterable) console.log(value);
        """;

    public const string AsyncIssueSource = """
        class C { static async *[Symbol.asyncIterator]() { yield 42; } }
        const iterable: any = C;
        async function run() {
            for await (const value of iterable) console.log(value);
        }
        run();
        """;

    public const string InheritedReceiverSource = """
        class Base {
            static *[Symbol.iterator]() { yield this; }
            static async *[Symbol.asyncIterator]() { await Promise.resolve(0); yield this; }
        }
        class Derived extends Base {}
        const expression: any = class {
            static *[Symbol.iterator]() { yield this; }
            static async *[Symbol.asyncIterator]() { await Promise.resolve(0); yield this; }
        };
        const base: any = Base, derived: any = Derived;
        for (const constructor of [base, derived, expression]) {
            for (const value of constructor) console.log(value === constructor);
        }
        async function run() {
            for (const constructor of [base, derived, expression]) {
                for await (const value of constructor) console.log(value === constructor);
            }
        }
        run();
        """;

    public const string ReceiverSource = """
        const iterable: any = class {
            static *[Symbol.iterator]() { yield this; }
            static async *[Symbol.asyncIterator]() { await Promise.resolve(0); yield this; }
        };
        for (const value of iterable) console.log(value === iterable);
        async function run() {
            for await (const value of iterable) console.log(value === iterable);
        }
        run();
        """;

    public const string GenericReceiverSource = """
        class Base<T> {
            static *[Symbol.iterator]() { yield this; }
            static async *[Symbol.asyncIterator]() { await Promise.resolve(0); yield this; }
        }
        class Derived<T> extends Base<T> {}
        class Override<T> extends Base<T> {
            static *[Symbol.iterator]() { yield this; }
            static async *[Symbol.asyncIterator]() { await Promise.resolve(0); yield this; }
        }
        const base: any = Base, derived: any = Derived, overridden: any = Override;
        for (const constructor of [base, derived, overridden]) {
            for (const value of constructor) console.log(value === constructor);
        }
        async function run() {
            for (const constructor of [base, derived, overridden]) {
                for await (const value of constructor) console.log(value === constructor);
            }
        }
        run();
        """;

    public const string AcquisitionReceiverSource = """
        let syncCalls = 0, asyncCalls = 0;
        function* syncValues(owner: any) { yield owner; }
        async function* asyncValues(owner: any) { await Promise.resolve(0); yield owner; }
        class C {
            static [Symbol.iterator](value: any = undefined) {
                syncCalls++;
                console.log(this === iterable, value === undefined);
                return syncValues(this);
            }
            static [Symbol.asyncIterator](value: any = undefined) {
                asyncCalls++;
                console.log(this === iterable, value === undefined);
                return asyncValues(this);
            }
        }
        const iterable: any = C;
        for (const value of iterable) console.log(value === iterable);
        console.log([...iterable][0] === iterable);
        const [first] = iterable;
        console.log(first === iterable);
        console.log(Array.from(iterable)[0] === iterable);
        function* delegated(owner: any) { yield* owner; }
        console.log(delegated(iterable).next().value === iterable);
        async function run() {
            for await (const value of iterable) console.log(value === iterable);
            console.log(syncCalls, asyncCalls);
        }
        run();
        """;

    public const string GetterSource = """
        function* syncValues() { yield 42; }
        async function* asyncValues() { yield 43; }
        class Base {
            static get [Symbol.iterator]() { console.log(this === Derived); return syncValues; }
            static get [Symbol.asyncIterator]() { console.log(this === Derived); return asyncValues; }
        }
        class Derived extends Base {}
        const iterable: any = Derived;
        for (const value of iterable) console.log(value);
        async function run() {
            for await (const value of iterable) console.log(value);
        }
        run();
        """;

    public const string FallbackSource = """
        class Preferred {
            static *[Symbol.iterator]() { yield "sync"; }
            static async *[Symbol.asyncIterator]() { yield "async"; }
        }
        class Fallback { static *[Symbol.iterator]() { yield Promise.resolve(42); } }
        const preferred: any = Preferred, fallback: any = Fallback;
        async function run() {
            for await (const value of preferred) console.log(value);
            for await (const value of fallback) console.log(value);
            fallback[Symbol.asyncIterator] = null;
            for await (const value of fallback) console.log(value);
            fallback[Symbol.asyncIterator] = undefined;
            for await (const value of fallback) console.log(value);
        }
        run();
        """;

    public const string CloseSource = """
        let closed = false;
        class C {
            static async *[Symbol.asyncIterator]() {
                try { yield 44; yield 45; } finally { closed = true; }
            }
        }
        const iterable: any = C;
        async function run() {
            for await (const value of iterable) break;
            console.log(closed);
        }
        run();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["sync", SyncIssueSource, "42\n"];
        yield return ["async", AsyncIssueSource, "42\n"];
        yield return ["receivers", ReceiverSource, "true\ntrue\n"];
        yield return ["fallback", FallbackSource, "async\n42\n42\n42\n"];
        yield return ["close", CloseSource, "true\n"];
        yield return ["spread", """
            class C { static *[Symbol.iterator]() { yield 42; yield 43; } }
            const iterable: any = C;
            console.log([...iterable].join(","));
            """, "42,43\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[1], row[2], mode }));

    public static IEnumerable<object[]> ReceiverCases()
    {
        yield return ["inherited", InheritedReceiverSource, string.Concat(Enumerable.Repeat("true\n", 6))];
        yield return ["generic", GenericReceiverSource, string.Concat(Enumerable.Repeat("true\n", 6))];
        yield return ["acquisition", AcquisitionReceiverSource,
            string.Concat(Enumerable.Repeat("true true\ntrue\n", 6)) + "5 1\n"];
    }

    public static IEnumerable<object[]> ReceiverParityCases()
        => ReceiverCases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ConstructorProtocolsUseTheClassReceiver(
        string source, string expected, ExecutionMode mode)
        => Assert.Equal(expected, TestHarness.Run(source, mode));

    [Theory, MemberData(nameof(ReceiverParityCases))]
    public void ProtocolMethodsReceiveTheAcquiredConstructor(
        string source, string expected, ExecutionMode mode)
        => Assert.Equal(expected, TestHarness.Run(source, mode));

    [Fact]
    public void InterpreterInvokesInheritedIteratorGettersWithTheConstructor()
        => Assert.Equal("true\n42\ntrue\n43\n",
            TestHarness.Run(GetterSource, ExecutionMode.Interpreted));

    [Theory]
    [InlineData("iterator")]
    [InlineData("asyncIterator")]
    public void InterpreterRejectsNoncallableConstructorProtocol(string protocol)
    {
        var source = $$"""
            class C {}
            const iterable: any = C;
            iterable[Symbol.{{protocol}}] = 7;
            async function run() {
                try {
                    {{(protocol == "iterator" ? "for (const value of iterable) {}" : "for await (const value of iterable) {}")}}
                } catch (error) { console.log(error instanceof TypeError); }
            }
            run();
            """;
        Assert.Equal("true\n", TestHarness.Run(source, ExecutionMode.Interpreted));
    }
}
