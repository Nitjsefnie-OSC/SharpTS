using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BoundArrayConstructorTests
{
    public const string IssueSource = """
        const bind: any = (() => 0).bind;
        const Bound: any = bind.call(Array, {}, 7, 8);
        console.log(Bound().join(","));
        console.log(new Bound().join(","));
        """;

    public const string ArgumentsSource = """
        const bind: any = (() => 0).bind, receiver = { marker: 1 }, item = { tag: 2 };
        const Empty: any = bind.call(Array, receiver), Length: any = bind.call(Array, receiver, 3);
        const Missing: any = bind.call(Array, receiver, undefined), Nil: any = bind.call(Array, receiver, null);
        const ElementArray: any = bind.call(Array, receiver, item);
        const empty = new Empty(), sparse = new Length();
        console.log(empty.length);
        console.log(sparse.length, Object.hasOwn(sparse, "0"), Object.hasOwn(sparse, "2"), Array.isArray(sparse));
        const missing = new Missing(), nil = new Nil();
        console.log(missing.length, Object.hasOwn(missing, "0"), missing[0] === undefined,
            nil.length, nil[0] === null);
        const first = new ElementArray(), second = new ElementArray();
        console.log(first[0] === item, second[0] === item, first !== second, Array.isArray(first));
        console.log(receiver.marker, Object.keys(receiver).join(","));
        """;

    public const string SpreadSource = """
        const bind: any = (() => 0).bind, state = { trace: "" };
        function prefix(value: number) { state.trace += "b"; return value; }
        const Once: any = bind.call(Array, {}, prefix(1)), Twice: any = Once.bind({}, prefix(2));
        function* args() {
            state.trace += "s"; yield 3;
            state.trace += "t"; yield 4;
            state.trace += "e";
        }
        function last() { state.trace += "l"; return 5; }
        const value = new Twice(...args(), last());
        console.log(value.join(","), state.trace);
        const next = new Twice();
        console.log(next.join(","), value !== next);
        const sentinel = {};
        function* broken() { state.trace += "x"; yield 6; state.trace += "y"; throw sentinel; }
        try { new Twice(...broken()); }
        catch (error) { console.log(state.trace, error === sentinel); }
        """;

    public const string InvalidLengthSource = """
        const bind: any = (() => 0).bind;
        const Negative: any = bind.call(Array, {}, -1), Fraction: any = bind.call(Array, {}, 1.5);
        const Empty: any = bind.call(Array, {});
        try { new Negative(); } catch (error) { console.log(error instanceof RangeError); }
        try { new Fraction(); } catch (error) { console.log(error instanceof RangeError); }
        try { new Empty(-1); } catch (error) { console.log(error instanceof RangeError); }
        const elements = new Negative(2);
        console.log(elements.length, elements[0], elements[1]);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "7,8\n7,8\n"];
        yield return ["arguments", ArgumentsSource,
            "0\n3 false false true\n1 true true 1 true\ntrue true true true\n1 marker\n"];
        yield return ["spread", SpreadSource, "1,2,3,4,5 bbstel\n1,2 true\nbbstelxy true\n"];
        yield return ["invalid-length", InvalidLengthSource, "true\ntrue\ntrue\n2 -1 2\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void BoundArrayConstructionPreservesGuestArgumentsAndErrors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
