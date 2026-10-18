using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NullishOnlyUnionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["annotated-return", """
            function choose(flag: boolean): null | undefined {
                return flag ? undefined : null;
            }
            console.log(choose(true) === undefined, choose(false) === null);
            console.log(typeof choose(true), typeof choose(false));
            """, "true true\nundefined object\n"];
        yield return ["inferred-return", """
            function choose(flag: boolean) { return flag ? undefined : null; }
            console.log(choose(true) === undefined, choose(false) === null);
            """, "true true\n"];
        yield return ["descriptor-getter", """
            const receiver: any = {};
            Object.defineProperty(receiver, "value", {
                get: function(this: any) { return this === receiver ? undefined : null; }
            });
            const getter: any = Object.getOwnPropertyDescriptor(receiver, "value")!.get;
            console.log(receiver.value === undefined, getter.call({}) === null);
            """, "true true\n"];
        yield return ["nested-duplicate-alias", """
            type Nothing = null | (undefined | null) | undefined;
            function choose(flag: boolean): Nothing { return flag ? undefined : null; }
            console.log(choose(true) === undefined, choose(false) === null);
            """, "true true\n"];
        yield return ["arrow-returns", """
            const expression = (flag: boolean): undefined | null => flag ? undefined : null;
            const block = (flag: boolean) => { return flag ? undefined : null; };
            console.log(expression(true) === undefined, expression(false) === null);
            console.log(block(true) === undefined, block(false) === null);
            """, "true true\ntrue true\n"];
        yield return ["mixed-nullable-controls", """
            function numberNull(flag: boolean): number | null { return flag ? null : 4; }
            function numberUndefined(flag: boolean): number | undefined { return flag ? undefined : 5; }
            function booleanNull(flag: boolean): boolean | null { return flag ? null : false; }
            function booleanUndefined(flag: boolean): boolean | undefined { return flag ? undefined : true; }
            console.log(numberNull(true) === null, numberNull(false));
            console.log(numberUndefined(true) === undefined, numberUndefined(false));
            console.log(booleanNull(true) === null, booleanNull(false));
            console.log(booleanUndefined(true) === undefined, booleanUndefined(false));
            """, "true 4\ntrue 5\ntrue false\ntrue true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void NullishReturnValuesRemainDistinct(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
