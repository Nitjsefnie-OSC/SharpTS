using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class LocalClassMethodCaptureTests
{
    public const string IssueSource = """
        function make(prefix: string) {
            class C { #m() { return prefix; } read() { return this.#m(); } }
            return new C();
        }
        console.log(make("outer").read());
        """;

    public const string DeclarationAndExpressionSource = """
        function make(prefix: string): any[] {
            class PublicDeclaration { read() { return prefix + ":public-decl"; } }
            class PrivateDeclaration {
                #m() { return prefix + ":private-decl"; }
                read() { return this.#m(); }
            }
            const PublicExpression = class { read() { return prefix + ":public-expr"; } };
            const PrivateExpression = class {
                #m() { return prefix + ":private-expr"; }
                read() { return this.#m(); }
            };
            const readers = [new PublicDeclaration(), new PrivateDeclaration(),
                new PublicExpression(), new PrivateExpression()];
            prefix += "!";
            return readers;
        }
        const left = make("left"), right = make("right");
        for (let index = 0; index < left.length; index++) {
            console.log(left[index].read(), right[index].read());
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "outer\n"];
        yield return ["declaration-expression", DeclarationAndExpressionSource,
            "left!:public-decl right!:public-decl\nleft!:private-decl right!:private-decl\n" +
            "left!:public-expr right!:public-expr\nleft!:private-expr right!:private-expr\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void LocalClassMethodsRetainTheirEnclosingParameter(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
