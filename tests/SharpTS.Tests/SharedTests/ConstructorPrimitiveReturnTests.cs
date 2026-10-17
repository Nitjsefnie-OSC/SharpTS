using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ConstructorPrimitiveReturnTests
{
    public const string IssueSource = """
        function Returned(this: any, value: any) { this.marker = 7; return value; }
        const target: any = Returned;
        for (const value of [Symbol('x'), 1n]) {
            const direct: any = new target(value);
            console.log(direct.marker, direct instanceof target);
            const reflected: any = Reflect.construct(target, [value]);
            console.log(reflected.marker, reflected instanceof target);
        }
        """;

    public const string BoundAndNewTargetSource = """
        function Returned(this: any, value: any) {
            this.marker = 7;
            this.actualTarget = new.target;
            return value;
        }
        function Alternate() {}
        const target: any = Returned, alternate: any = Alternate, ignored = { marker: 0 };
        for (const value of [Symbol("bound"), 2n, 0n]) {
            const Bound: any = target.bind(ignored, value);
            const bound: any = new Bound();
            console.log(bound.marker, bound.actualTarget === target,
                Object.getPrototypeOf(bound) === target.prototype, bound instanceof target);
            const redirected: any = Reflect.construct(target, [value], alternate);
            console.log(redirected.marker, redirected.actualTarget === alternate,
                Object.getPrototypeOf(redirected) === alternate.prototype, redirected instanceof alternate);
        }
        console.log(ignored.marker);
        """;

    public const string ReplacementIdentitySource = """
        function Returned(this: any, value: any) { this.marker = 7; return value; }
        const target: any = Returned;
        const object = { tag: "object" }, callable = () => "callable";
        for (const replacement of [object, callable, Object(Symbol("boxed")), Object(0n)]) {
            const Bound: any = target.bind(null, replacement);
            console.log(new target(replacement) === replacement,
                Reflect.construct(target, [replacement]) === replacement,
                new Bound() === replacement);
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "7 true\n7 true\n7 true\n7 true\n"];
        yield return ["bound-and-new-target", BoundAndNewTargetSource,
            string.Concat(Enumerable.Repeat("7 true true true\n", 6)) + "0\n"];
        yield return ["replacement-identity", ReplacementIdentitySource,
            string.Concat(Enumerable.Repeat("true true true\n", 4))];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OrdinaryConstructionIgnoresSymbolAndBigIntReturnsAndKeepsObjectReplacements(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
