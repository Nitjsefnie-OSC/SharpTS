using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class SwitchStrictEqualityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["scalars-and-identity", """
            function select(value: any): string {
                switch (value) {
                    case undefined: return "undefined";
                    case null: return "null";
                    case "0": return "string";
                    case false: return "boolean";
                    case 0: return "number";
                    case NaN: return "bad NaN";
                    default: return "other";
                }
            }
            console.log(select(null), select(undefined), select(0), select("0"), select(false), select(NaN));
            let calls = 0;
            const object: any = { valueOf() { calls++; return 0; } };
            const other: any = {};
            function identity(value: any): string {
                switch (value) {
                    case 0: return "coerced";
                    case other: return "other";
                    case object: return "object";
                    default: return "neither";
                }
            }
            console.log(identity(object), identity(other), calls);
            """, "null undefined number string boolean other\nobject other 0\n"];
        yield return ["resumable", """
            async function asynchronous(value: any): Promise<string> {
                await Promise.resolve(0);
                switch (value) {
                    case undefined: return "undefined";
                    case null: return "null";
                    case false: return "boolean";
                    case "0": return "string";
                    case 0: return "number";
                    case NaN: return "bad NaN";
                    default: return "other";
                }
            }
            function* generator(value: any): any {
                yield "start";
                switch (value) {
                    case undefined: yield "undefined"; break;
                    case null: yield "null"; break;
                    case false: yield "boolean"; break;
                    case "0": yield "string"; break;
                    case 0: yield "number"; break;
                    case NaN: yield "bad NaN"; break;
                    default: yield "other";
                }
            }
            async function run() {
                console.log(await asynchronous(null), await asynchronous(undefined), await asynchronous(0), await asynchronous(NaN));
                const first: any = generator(null), second: any = generator(0);
                console.log(first.next().value, first.next().value, first.next().done);
                console.log(second.next().value, second.next().value, second.next().done);
            }
            run().catch((error: any) => console.log("unexpected", error));
            """, "null undefined number other\nstart null true\nstart number true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void CasesUseStrictEquality(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
