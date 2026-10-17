using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InferredClassFieldRuntimeTests
{
    public const string EffectsAndStorageSource = """
        const state = { events: [] as string[], calls: 0 };
        function initialize(tag: string, value: number): number { state.events.push(tag); return value; }
        class Model {
            static first = initialize("s1", 1);
            static { state.events.push("block"); }
            static second = initialize("s2", 2);
            value = initialize("i", 3);
            flag = true;
            callback = (value: number) => { state.calls++; return this.value + value; };
            constructor() { state.events.push("body"); }
        }
        const Expression = class {
            static value = initialize("es", 4);
            value = initialize("ei", 5);
        };
        console.log(state.events.join("|"));
        const first = new Model(), second = new Model(), expression = new Expression();
        console.log(state.events.join("|"), first.callback(1), second.callback(2), expression.value, state.calls);
        first.value = 6; first.flag = false;
        console.log(first.callback(1), second.callback(1), first.flag, second.flag, state.calls);
        const raw: any = {}, alias: any = first;
        alias.value = raw; alias.flag = null;
        console.log(Object.is(first.value, raw), first.flag === null, second.value, second.flag, state.calls);
        alias.value = undefined;
        console.log(first.value === undefined);
        """;

    public const string GenericCallableSource = """
        class Box<T> {
            callback = (value: T): T => value;
            self = () => this;
        }
        const Expression = class<T> { callback = (value: T): T => value; };
        const numbers = new Box<number>(), words = new Box<string>();
        const expressionNumbers = new Expression<number>(), expressionWords = new Expression<string>();
        console.log(numbers.callback(7), words.callback("a"), expressionNumbers.callback(8),
            expressionWords.callback("b"), numbers.self() === numbers);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["effects-and-storage", EffectsAndStorageSource,
            "s1|block|s2|es\ns1|block|s2|es|i|body|i|body|ei 4 5 5 2\n7 4 false true 4\ntrue true 3 true 4\ntrue\n"];
        yield return ["generic-callables", GenericCallableSource, "7 a 8 b true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InferredFieldsPreserveInitializationEffectsCallableValuesAndRawStorage(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
