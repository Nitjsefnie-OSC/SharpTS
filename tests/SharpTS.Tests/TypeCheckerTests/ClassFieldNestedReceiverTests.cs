using SharpTS.Diagnostics;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class ClassFieldNestedReceiverTests
{
    private static IReadOnlyList<Diagnostic> Diagnostics(string source)
    {
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        return new TypeChecker(maxErrors: 50).CheckWithRecovery(statements).Diagnostics;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NestedMethodsAndAccessorsUseTheirOwnReceiverAndRestoreInitializerThis(
        bool expression, bool staticField)
    {
        string header = expression ? "const Outer = class Outer {" : "class Outer {";
        string modifier = staticField ? "static " : "";
        string source = $$"""
            {{header}}
                {{modifier}}seed = 10;
                {{modifier}}Inner = (() => {
                    const Inner = class Inner {
                        static #staticValue = 1;
                        #instanceValue = 2;
                        static read(): number { return this.#staticValue; }
                        read(): number { return this.#instanceValue; }
                        static get value(): number { return this.#staticValue; }
                        get value(): number { return this.#instanceValue; }
                        static set value(value: number) { this.#staticValue = value; }
                        set value(value: number) { this.#instanceValue = value; }
                    };
                    const restored: number = this.seed;
                    return Inner;
                })();
            };
            """;
        Assert.Empty(Diagnostics(source));
    }

    [Theory]
    [InlineData(false, "read(): string { return this.#value; }")]
    [InlineData(true, "read(): string { return this.#value; }")]
    [InlineData(false, "get read(): string { return this.#value; }")]
    [InlineData(true, "get read(): string { return this.#value; }")]
    [InlineData(false, "set read(value: string) { this.#value = value; }")]
    [InlineData(true, "set read(value: string) { this.#value = value; }")]
    public void NestedReceiverMembersKeepTheirOwnInferredTypes(bool isStatic, string member)
    {
        string modifier = isStatic ? "static " : "";
        string source = $$"""
            class Outer {
                Inner = class Inner {
                    {{modifier}}#value = 1;
                    {{modifier}}{{member}}
                };
            }
            """;
        var diagnostic = Assert.Single(Diagnostics(source));
        Assert.Equal("TS2322", diagnostic.TsCode);
        Assert.Equal(4, diagnostic.Line);
    }
}
