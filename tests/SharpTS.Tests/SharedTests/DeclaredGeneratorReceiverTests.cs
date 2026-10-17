using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DeclaredGeneratorReceiverTests
{
    public const string SyncSource = """
        function* values(this: any) { yield this; }
        const receiver: any = { values };
        const generator: any = values;
        console.log(receiver.values().next().value === receiver);
        console.log(generator.call(receiver).next().value === receiver);
        const extracted: any = receiver.values, copied: any = { values: extracted };
        console.log(receiver.values === values, receiver["values"] === values,
            receiver.values === receiver.values, extracted === values, copied.values === values);
        console.log(copied.values().next().value === copied, receiver["values"]().next().value === receiver);
        console.log(extracted.apply(receiver, []).next().value === receiver,
            extracted.bind(receiver)().next().value === receiver);
        """;

    public const string AsyncSource = """
        async function* values(this: any) { yield this; }
        async function same(generator: any, receiver: any) {
            const first = await generator.next();
            await generator.next();
            return first.value === receiver;
        }
        async function run() {
            const receiver: any = { values }, copied: any = { values: receiver.values };
            const extracted: any = receiver.values;
            console.log(receiver.values === values, receiver["values"] === values,
                receiver.values === receiver.values, copied.values === values, extracted === values);
            console.log(await same(receiver.values(), receiver), await same(receiver["values"](), receiver));
            console.log(await same(copied.values(), copied), await same(extracted.call(receiver), receiver));
            console.log(await same(extracted.apply(receiver, []), receiver), await same(extracted.bind(receiver)(), receiver));
        }
        run();
        """;

    public const string ProtocolSource = """
        const state: any = { syncGets: 0, asyncGets: 0, syncCalls: 0, asyncCalls: 0, valid: true, expected: null };
        function* values(this: any) { state.syncCalls++; yield this; }
        async function* asyncValues(this: any) { state.asyncCalls++; yield this; }
        class Base {
            static get [Symbol.iterator]() {
                state.syncGets++; state.valid = state.valid && this === state.expected;
                return values;
            }
            static get [Symbol.asyncIterator]() {
                state.asyncGets++; state.valid = state.valid && this === state.expected;
                return asyncValues;
            }
        }
        class Derived extends Base {}
        const expression: any = class {
            static get [Symbol.iterator]() {
                state.syncGets++; state.valid = state.valid && this === state.expected;
                return values;
            }
            static get [Symbol.asyncIterator]() {
                state.asyncGets++; state.valid = state.valid && this === state.expected;
                return asyncValues;
            }
        };
        const owners: any[] = [Derived, expression];
        for (const owner of owners) {
            state.expected = owner;
            for (const value of owner) console.log(value === owner);
        }
        async function run() {
            for (const owner of owners) {
                state.expected = owner;
                for await (const value of owner) console.log(value === owner);
            }
            console.log(state.syncGets, state.syncCalls, state.asyncGets, state.asyncCalls, state.valid);
        }
        run();
        """;

    public const string CapturesAndArgumentsSource = """
        function make(offset: number) {
            function* values(this: any, first: any, ...rest: any[]) {
                yield [this, offset, first, rest.length, arguments.length, arguments[1]];
            }
            async function* asyncValues(this: any, first: any, ...rest: any[]) {
                yield [this, offset, first, rest.length, arguments.length, arguments[1]];
            }
            return { values, asyncValues };
        }
        const receiver: any = make(9), generator = receiver.values("a", "b");
        const value = generator.next().value;
        generator.next();
        console.log(value[0] === receiver, value.slice(1).join(","));
        async function run() {
            const generator = receiver.asyncValues("c", "d");
            const value = (await generator.next()).value;
            await generator.next();
            console.log(value[0] === receiver, value.slice(1).join(","));
        }
        run();
        """;

    public const string GlobalArgumentsSource = """
        const offset = 9;
        function* values(this: any, first: any, ...rest: any[]) {
            yield [this, offset, first, rest.length, arguments.length, arguments[1]];
        }
        async function* asyncValues(this: any, first: any, ...rest: any[]) {
            yield [this, offset, first, rest.length, arguments.length, arguments[1]];
        }
        const receiver: any = { values, asyncValues }, generator = receiver.values("a", "b");
        const value = generator.next().value;
        generator.next();
        console.log(value[0] === receiver, value.slice(1).join(","));
        async function run() {
            const generator = receiver.asyncValues("c", "d");
            const value = (await generator.next()).value;
            await generator.next();
            console.log(value[0] === receiver, value.slice(1).join(","));
        }
        run();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["sync", SyncSource, "true\ntrue\ntrue true true true true\ntrue true\ntrue true\n"];
        yield return ["async", AsyncSource, "true true true true true\ntrue true\ntrue true\ntrue true\n"];
        yield return ["protocols", ProtocolSource, "true\ntrue\ntrue\ntrue\n2 2 2 2 true\n"];
        yield return ["global-captures-and-arguments", GlobalArgumentsSource, "true 9,a,1,2,b\ntrue 9,c,1,2,d\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DeclaredGeneratorsBindOnlyAtInvocationAndPreserveTheirClosure(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedNestedDeclarationsPreserveTheirClosureAndArguments()
    {
        // Capturing nested generators with arguments/rest need separate compiled lowering (#2326).
        Assert.Equal("true 9,a,1,2,b\ntrue 9,c,1,2,d\n", TestHarness.RunInterpreted(CapturesAndArgumentsSource));
    }
}
