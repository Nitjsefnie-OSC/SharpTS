using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArrayConstructorValueTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["alias-and-member", """
            const Constructor: any = Array;
            const holder: any = { Constructor: Array };
            const empty: any = new Constructor();
            const sized: any = new Constructor(3);
            const element: any = new Constructor("3");
            const many: any = new holder.Constructor(1, 2, 3);
            console.log(Array.isArray(empty), empty.length);
            console.log(Array.isArray(sized), sized.length, sized[0], Object.hasOwn(sized, "0"));
            console.log(element.length, element[0]);
            console.log(many.length, many.join("-"));
            const numbers: any = [1, 2];
            const spread: any = new Constructor(...numbers, 3);
            console.log(spread.length, spread.join("-"));
            try { new Constructor(-1); } catch (error: any) { console.log(error.name); }
            """, "true 0\ntrue 3 undefined false\n1 3\n3 1-2-3\n3 1-2-3\nRangeError\n"];
        yield return ["evaluation-order", """
            let trace = "";
            const holder: any = { Constructor: Array };
            function replacement(): any { trace += "A"; holder.Constructor = function() { trace += "F"; }; return 2; }
            const selected: any = new holder.Constructor(replacement());
            console.log(Array.isArray(selected), selected.length, trace);
            const token: any = {};
            function fail(): any { trace += "X"; throw token; }
            try { new (Array as any)(fail(), (trace += "late")); }
            catch (error) { console.log(error === token, trace); }
            """, "true 2 A\ntrue AX\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ConstructorValuesPreserveArrayArgumentsAndEvaluationOrder(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
