using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BorrowedBindReceiverTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class C { static receiver() { return this; } }
            const f: any = C.receiver;
            const bind: any = Function.prototype.bind;
            const omitted: any = Reflect.apply(bind, f, []);
            const explicitNull: any = Reflect.apply(bind, f, [null]);
            console.log(omitted() === undefined, omitted() === null);
            console.log(explicitNull() === undefined, explicitNull() === null);
            console.log(f.bind()() === undefined);
            """, "true false\nfalse true\ntrue\n"];
        yield return ["controls", """
            class C {
                static receiver() { return this; }
                static pair(a: any, b: any) { return [this, a, b]; }
            }
            const f: any = C.receiver, bind: any = Function.prototype.bind;
            const receiver: any = { label: "kept" };
            console.log(bind.call(f)() === undefined,
                Reflect.apply(bind, f, [undefined])() === undefined,
                Reflect.apply(bind, f, [null])() === null);
            console.log(Reflect.apply(bind, f, [receiver])() === receiver,
                Reflect.apply(bind, f, [7])() === 7,
                Reflect.apply(bind, f, [false])() === false);
            const first: any = Reflect.apply(bind, f, []);
            const again: any = Reflect.apply(bind, first, [receiver]);
            console.log(again() === undefined);
            const partial: any = Reflect.apply(bind, C.pair, [receiver, 1]);
            const pair: any = partial(2);
            console.log(pair[0] === receiver, pair[1], pair[2]);
            """, "true true true\ntrue true true\ntrue\ntrue 1 2\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void BorrowedBindPreservesOmittedAndExplicitReceivers(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
