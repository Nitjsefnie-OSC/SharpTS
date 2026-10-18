using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OptionalConstructorParameterValueTests
{
    public const string Exact = """
        class C {
            kind: string;
            constructor(value?: string) {
                this.kind = value === undefined ? "missing" : value === null ? "null" : value;
            }
        }
        try { console.log("omitted", new C().kind); } catch { console.log("omitted rejected"); }
        try { console.log("undefined", new C(undefined).kind); } catch { console.log("undefined rejected"); }
        console.log("null", new C(null as any).kind);
        console.log("text", new C("ok").kind);
        const Constructor: any = C;
        try { console.log("alias", new Constructor().kind); } catch { console.log("alias rejected"); }
        """;

    public const string DerivedDefaultAndRest = """
        function kind(value: any): string {
            return value === undefined ? "missing" : value === null ? "null" : value;
        }
        class Base {
            value: any;
            constructor(value?: string) { this.value = value; }
        }
        class Derived extends Base {
            constructor(value?: string, public label: any = "fallback", ...tail: string[]) {
                super(value);
                console.log(kind(value), kind(this.value), kind(this.label), tail.length);
            }
        }
        new Derived();
        new Derived(undefined, undefined);
        new Derived(null as any, null as any, "tail");
        new Derived("ok", "named", "x", "y");
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-optional-string", Exact, "omitted missing\nundefined missing\nnull null\ntext ok\nalias missing\n"];
        yield return ["derived-default-and-rest", DerivedDefaultAndRest, "missing missing fallback 0\nmissing missing fallback 0\nnull null null 1\nok ok named 2\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OptionalConstructorParametersPreserveRawValues(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
