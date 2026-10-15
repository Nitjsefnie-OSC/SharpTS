using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class EmptyClassElementTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var declaration in new[] { "class C", "const C = class" })
            yield return [declaration, $$"""
                {{declaration}} {
                    ;;;
                    field = 8;;;
                    constructor() {};;;
                    async method() { return 4; };;;
                    *values() { yield 5; };;;
                    get value() { return this.field; };;;
                    set value(value: number) { this.field = value; };;;
                    static read() { return 7; };;;
                    static {};;;
                };
                const value = new C();
                value.value = 9;
                console.log(value.method() instanceof Promise, value.values().next().value,
                    value.value, C.read());
                """];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void EmptyElementsCanSurroundEveryKindOfClassMember(string declaration, string source, ExecutionMode mode)
    {
        Assert.NotEmpty(declaration);
        Assert.Equal("true 5 9 7\n", TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("abstract class C { ;;; abstract method(): number;;; read() { return 1; };;; }")]
    [InlineData("declare class C { ;;; method(): number;;; }")]
    [InlineData("class C { ;;; } const D = class { ;;; };")]
    public void EmptyElementsAreAlsoAcceptedInAbstractAmbientAndEmptyClasses(string source)
        => Assert.True(new Parser(new Lexer(source).ScanTokens()).Parse().IsSuccess);

    [Fact]
    public void SemicolonDoesNotDiscardAPendingDecorator()
        => Assert.False(new Parser(new Lexer("class C { @decorator ; }").ScanTokens(),
            DecoratorMode.Stage3).Parse().IsSuccess);
}
