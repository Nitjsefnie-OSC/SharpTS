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

    public const string GetterReceiverSource = """
        let syncGets = 0, asyncGets = 0;
        let expected: any;
        function* syncValues(owner: any) { yield owner; }
        async function* asyncValues(owner: any) { await Promise.resolve(0); yield owner; }
        function syncFactory() { return syncValues(this); }
        function asyncFactory() { return asyncValues(this); }
        class Base {
            static get [Symbol.iterator]() { syncGets++; console.log(this === expected); return syncFactory; }
            static get [Symbol.asyncIterator]() { asyncGets++; console.log(this === expected); return asyncFactory; }
        }
        class Derived extends Base {}
        class Generic<T> {
            static get [Symbol.iterator]() { syncGets++; console.log(this === expected); return syncFactory; }
            static get [Symbol.asyncIterator]() { asyncGets++; console.log(this === expected); return asyncFactory; }
        }
        class GenericDerived<T> extends Generic<T> {}
        const expression: any = class {
            static get [Symbol.iterator]() { syncGets++; console.log(this === expected); return syncFactory; }
            static get [Symbol.asyncIterator]() { asyncGets++; console.log(this === expected); return asyncFactory; }
        };
        const owners: any[] = [Base, Derived, Generic, GenericDerived, expression];
        for (const owner of owners) {
            expected = owner;
            for (const value of owner) console.log(value === owner);
        }
        async function run() {
            for (const owner of owners) {
                expected = owner;
                for await (const value of owner) console.log(value === owner);
            }
            console.log(syncGets, asyncGets);
        }
        run();
        """;

    public const string GetterAcquisitionSource = """
        let syncGets = 0, asyncGets = 0, syncCalls = 0, asyncCalls = 0;
        let receiversValid = true;
        function* syncValues(owner: any) { yield owner; yield owner; }
        async function* asyncValues(owner: any) { await Promise.resolve(0); yield owner; yield owner; }
        function syncFactory() { syncCalls++; return syncValues(this); }
        function asyncFactory() { asyncCalls++; return asyncValues(this); }
        class Base {
            static get [Symbol.iterator]() {
                syncGets++; receiversValid = receiversValid && this === iterable;
                return syncFactory;
            }
            static get [Symbol.asyncIterator]() {
                asyncGets++; receiversValid = receiversValid && this === iterable;
                return asyncFactory;
            }
        }
        class Derived extends Base {}
        const iterable: any = Derived;
        for (const value of iterable) console.log(value === iterable);
        console.log([...iterable].every((value: any) => value === iterable));
        const [first] = iterable;
        console.log(first === iterable);
        async function run() {
            for await (const value of iterable) console.log(value === iterable);
            console.log(syncGets, syncCalls, asyncGets, asyncCalls, receiversValid);
        }
        run();
        """;

    public const string GetterArrayFromSource = """
        let gets = 0, calls = 0;
        function* values(owner: any) { yield owner; }
        function factory() { calls++; return values(this); }
        class Base { static get [Symbol.iterator]() { gets++; return factory; } }
        class Derived extends Base {}
        const iterable: any = Derived;
        const result: any = Array.from(iterable);
        console.log(result[0] === iterable, gets, calls);
        """;

    public const string GetterErrorSource = """
        const marker = {};
        let syncGets = 0, asyncGets = 0;
        class Base {
            static get [Symbol.iterator]() { syncGets++; throw marker; }
            static get [Symbol.asyncIterator]() { asyncGets++; throw marker; }
        }
        class Derived extends Base {}
        const iterable: any = Derived;
        try { for (const value of iterable) {} }
        catch (error) { console.log(error === marker, syncGets, asyncGets); }
        async function run() {
            try { for await (const value of iterable) {} }
            catch (error) { console.log(error === marker, syncGets, asyncGets); }
        }
        run();
        """;

    public const string GetterInvalidSource = """
        let selected: any, syncGets = 0, asyncGets = 0;
        class Base {
            static get [Symbol.iterator]() { syncGets++; return selected; }
            static get [Symbol.asyncIterator]() { asyncGets++; return selected; }
        }
        class Derived extends Base {}
        const iterable: any = Derived;
        for (const method of [7, {}]) {
            selected = method;
            const before = syncGets;
            try { for (const value of iterable) {} }
            catch (error) { console.log(error instanceof TypeError, syncGets === before + 1); }
        }
        async function run() {
            for (const method of [7, {}]) {
                selected = method;
                const before = asyncGets;
                try { for await (const value of iterable) {} }
                catch (error) { console.log(error instanceof TypeError, asyncGets === before + 1); }
            }
        }
        run();
        """;

    public const string GetterMissingSource = """
        let selected: any, gets = 0;
        class Base { static get [Symbol.iterator]() { gets++; return selected; } }
        class Derived extends Base {}
        const expression: any = class { static get [Symbol.iterator]() { gets++; return selected; } };
        for (const iterable of [Derived, expression] as any[]) {
            for (const method of [null, undefined]) {
                selected = method;
                const before = gets;
                try { for (const value of iterable) {} }
                catch (error) { console.log(error instanceof TypeError, gets === before + 1); }
            }
        }
        """;

    public const string MissingRequiredConsumersSource = """
        const state = { gets: 0 };
        class C { static get [Symbol.iterator]() { state.gets++; return undefined; } }
        const iterable: any = C;
        const beforeSpread = state.gets;
        try { const values = [...iterable]; }
        catch (error) { console.log(error instanceof TypeError, state.gets === beforeSpread + 1); }
        const beforeDestructure = state.gets;
        try { const [first] = iterable; }
        catch (error) { console.log(error instanceof TypeError, state.gets === beforeDestructure + 1); }
        async function run() {
            await Promise.resolve(0);
            const beforeLoop = state.gets;
            try { for (const value of iterable) {} }
            catch (error) { console.log(error instanceof TypeError, state.gets === beforeLoop + 1); }
            const beforeAsyncLoop = state.gets;
            try { for await (const value of iterable) {} }
            catch (error) { console.log(error instanceof TypeError, state.gets === beforeAsyncLoop + 1); }
        }
        run();
        """;

    public const string MissingArrayLikeFallbackSource = """
        const state: any = { gets: 0, selected: null };
        const input: any = {
            0: "a", 1: "b", length: 2,
            get [Symbol.iterator]() { state.gets++; return state.selected; }
        };
        for (const method of [null, undefined]) {
            state.selected = method;
            const before = state.gets, values = Array.from(input);
            console.log(values.join(","), state.gets === before + 1);
        }
        """;

    public const string PlainArrayLikeFallbackSource = """
        const input: any = { 0: "a", 1: "b", length: 2 };
        console.log(Array.from(input).join(","));
        """;

    public const string GetterOverrideSource = """
        let syncGets = 0, asyncGets = 0;
        function* syncValues() { yield {} as any; }
        async function* asyncValues() { yield {} as any; }
        class Base {
            static get [Symbol.iterator]() { syncGets++; return syncValues; }
            static get [Symbol.asyncIterator]() { asyncGets++; return asyncValues; }
        }
        class Override extends Base {
            static *[Symbol.iterator]() { yield this; }
            static async *[Symbol.asyncIterator]() { yield this; }
        }
        class Derived extends Override {}
        const owners: any[] = [Override, Derived];
        for (const owner of owners) {
            for (const value of owner) console.log(value === owner);
        }
        async function run() {
            for (const owner of owners) {
                for await (const value of owner) console.log(value === owner);
            }
            console.log(syncGets, asyncGets);
        }
        run();
        """;

    public const string GetterFallbackSource = """
        let selected: any, syncGets = 0, asyncGets = 0;
        function* values(owner: any) { yield Promise.resolve(owner); }
        function factory() { return values(this); }
        class Base {
            static get [Symbol.iterator]() { syncGets++; return factory; }
            static get [Symbol.asyncIterator]() { asyncGets++; return selected; }
        }
        class Derived extends Base {}
        const iterable: any = Derived;
        async function run() {
            for (const method of [null, undefined]) {
                selected = method;
                for await (const value of iterable) console.log(value === iterable);
            }
            console.log(syncGets, asyncGets);
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

    public static IEnumerable<object[]> GetterCases()
    {
        yield return ["inherited", GetterSource, "true\n42\ntrue\n43\n"];
        yield return ["receivers", GetterReceiverSource, string.Concat(Enumerable.Repeat("true\n", 20)) + "5 5\n"];
        yield return ["acquisition", GetterAcquisitionSource, string.Concat(Enumerable.Repeat("true\n", 6)) + "3 3 1 1 true\n"];
        yield return ["errors", GetterErrorSource, "true 1 0\ntrue 1 1\n"];
        yield return ["invalid", GetterInvalidSource, string.Concat(Enumerable.Repeat("true true\n", 4))];
        yield return ["override", GetterOverrideSource, "true\ntrue\ntrue\ntrue\n0 0\n"];
        yield return ["fallback", GetterFallbackSource, "true\ntrue\n2 2\n"];
    }

    public static IEnumerable<object[]> GetterParityCases()
        => GetterCases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[1], row[2], mode }));

    public static IEnumerable<object[]> CompiledGetterCases()
        => GetterCases().Concat(new[] {
            new object[] { "missing", GetterMissingSource, string.Concat(Enumerable.Repeat("true true\n", 4)) },
            new object[] { "array-from", GetterArrayFromSource, "true 1 1\n" }
        }).Concat(MissingConsumerCases().Where(row => (string)row[0] != "missing-required-consumers"));

    public static IEnumerable<object[]> MissingConsumerCases()
    {
        yield return ["missing-required-consumers", MissingRequiredConsumersSource,
            string.Concat(Enumerable.Repeat("true true\n", 4))];
        yield return ["missing-array-like-fallback", MissingArrayLikeFallbackSource, "a,b true\na,b true\n"];
        yield return ["plain-array-like-fallback", PlainArrayLikeFallbackSource, "a,b\n"];
    }

    public static IEnumerable<object[]> MissingConsumerModeCases()
        // Compiled missing-class spread/async for-of/for-await are #2271, #2322 and #2323.
        => MissingConsumerCases().SelectMany(row => ((string)row[0] switch {
                "missing-required-consumers" => new[] { ExecutionMode.Interpreted },
                _ => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ConstructorProtocolsUseTheClassReceiver(
        string source, string expected, ExecutionMode mode)
        => Assert.Equal(expected, TestHarness.Run(source, mode));

    [Theory, MemberData(nameof(ReceiverParityCases))]
    public void ProtocolMethodsReceiveTheAcquiredConstructor(
        string source, string expected, ExecutionMode mode)
        => Assert.Equal(expected, TestHarness.Run(source, mode));

    [Theory, MemberData(nameof(GetterParityCases))]
    public void IteratorGettersAndTheirFactoriesReceiveTheAcquiredConstructor(
        string source, string expected, ExecutionMode mode)
        => Assert.Equal(expected, TestHarness.Run(source, mode));

    [Theory, ModeData]
    public void MissingIteratorGettersAreAcquiredOnceAndThrowTypeError(ExecutionMode mode)
        => Assert.Equal(string.Concat(Enumerable.Repeat("true true\n", 4)), TestHarness.Run(GetterMissingSource, mode));

    [Theory, MemberData(nameof(MissingConsumerModeCases))]
    public void MissingIteratorConsumersRequireGuestTypeErrorsOrArrayLikeFallback(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ArrayFromAcquiresConstructorIteratorGettersOnce(ExecutionMode mode)
        => Assert.Equal("true 1 1\n", TestHarness.Run(GetterArrayFromSource, mode));

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
