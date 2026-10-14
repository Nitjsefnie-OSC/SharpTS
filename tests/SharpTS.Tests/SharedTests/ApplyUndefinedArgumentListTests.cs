using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ApplyUndefinedArgumentListTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class C { static receiver() { return this; } }
            const f: any = C.receiver;
            try { console.log(f.apply(7, undefined) === 7); } catch (e) { console.log("threw"); }
            try { console.log(Reflect.apply(Function.prototype.apply, f, [7, undefined]) === 7); }
            catch (e) { console.log("threw"); }
            console.log(f.apply(7, null) === 7, f.apply(7, []) === 7);
            """, "true\ntrue\ntrue true\n"];
        yield return ["controls", """
            class C { static inspect(...values: any[]) { return [this, values.length, values.join(",")]; } }
            const f: any = C.inspect, receiver: any = {};
            const apply: any = Function.prototype.apply;
            console.log(f.apply(receiver)[1], f.apply(receiver, undefined)[1],
                f.apply(receiver, null)[1], f.apply(receiver, [])[1]);
            console.log(f.apply(receiver, undefined)[0] === receiver,
                Reflect.apply(apply, f, [receiver, undefined])[0] === receiver);
            const populated: any = f.apply(receiver, [1, 2]);
            const borrowed: any = Reflect.apply(apply, f, [receiver, [3, 4]]);
            console.log(populated[0] === receiver, populated[1], populated[2]);
            console.log(borrowed[0] === receiver, borrowed[1], borrowed[2]);
            """, "0 0 0 0\ntrue true\ntrue 2 1,2\ntrue 2 3,4\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ApplyNormalizesUndefinedArgumentListsWithoutChangingReceiversOrArrays(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedApplyStillRejectsInvalidPrimitiveListsWithoutCallingTarget()
    {
        const string source = """
            let calls = 0;
            const f: any = () => { calls++; };
            for (const list of [7, false, "bad"]) {
                try { f.apply(null, list); console.log(false); }
                catch (error) { console.log(true); }
            }
            try { Reflect.apply(Function.prototype.apply, f, [null, 7]); console.log(false); }
            catch (error) { console.log(true); }
            console.log(calls);
            """;
        Assert.Equal("true\ntrue\ntrue\ntrue\n0\n", TestHarness.RunInterpreted(source));
    }
}
