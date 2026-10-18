using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.ParserTests;

public sealed class ClassExpressionOverrideParserTests
{
    [Theory]
    [InlineData("override value() { return 2; }", false, false, false, false)]
    [InlineData("public override value() { return 2; }", false, false, false, false)]
    [InlineData("static override value() { return 2; }", true, false, false, false)]
    [InlineData("override async value() { return 2; }", false, true, false, false)]
    [InlineData("static override async value() { return 2; }", true, true, false, false)]
    [InlineData("override *value() { yield 2; }", false, false, true, false)]
    [InlineData("override async *value() { yield 2; }", false, true, true, false)]
    [InlineData("override [key]() { return 2; }", false, false, false, true)]
    [InlineData("static override async *[key]() { yield 2; }", true, true, true, true)]
    public void MethodModifiersKeepTheirFlags(
        string member, bool isStatic, bool isAsync, bool isGenerator, bool isComputed)
    {
        var method = Assert.Single(ParseClass("const Derived = class extends Base { " + member + " }; ").Methods);
        Assert.True(method.IsOverride);
        Assert.Equal(isStatic, method.IsStatic);
        Assert.Equal(isAsync, method.IsAsync);
        Assert.Equal(isGenerator, method.IsGenerator);
        Assert.Equal(isComputed, method.ComputedKey is not null);
    }

    [Theory]
    [InlineData("override get value(): number { return 2; }", false, false)]
    [InlineData("override set value(next: number) {}", false, false)]
    [InlineData("static override get value(): number { return 2; }", true, false)]
    [InlineData("static override set value(next: number) {}", true, false)]
    [InlineData("override get [key](): number { return 2; }", false, true)]
    [InlineData("override set [key](next: number) {}", false, true)]
    public void AccessorsKeepTheOverrideFlag(string member, bool isStatic, bool isComputed)
    {
        var accessor = Assert.Single(ParseClass("const Derived = class extends Base { " + member + " };").Accessors!);
        Assert.True(accessor.IsOverride);
        Assert.Equal(isStatic, accessor.IsStatic);
        Assert.Equal(isComputed, accessor.ComputedKey is not null);
    }

    [Theory]
    [InlineData("override accessor value: number = 2;", false)]
    [InlineData("static override accessor value: number = 2;", true)]
    public void AutoAccessorsKeepTheOverrideFlag(string member, bool isStatic)
    {
        var accessor = Assert.Single(ParseClass("const Derived = class extends Base { " + member + " };").AutoAccessors!);
        Assert.True(accessor.IsOverride);
        Assert.Equal(isStatic, accessor.IsStatic);
    }

    [Fact]
    public void OverrideFieldsFollowTheDeclarationFieldRepresentation()
    {
        var expression = ParseClass("const Derived = class extends Base { override value: number = 2; };");
        Assert.Equal("value", Assert.Single(expression.Fields).Name.Lexeme);
    }

    [Theory]
    [InlineData("override() { return 1; }")]
    [InlineData("static override() { return 1; }")]
    [InlineData("override<T>(value: T): T { return value; }")]
    [InlineData("override = 1;")]
    [InlineData("override: number = 1;")]
    [InlineData("override;")]
    [InlineData("override?: number;")]
    [InlineData("override!: number;")]
    [InlineData("override")]
    [InlineData("static override")]
    public void OverrideCanRemainALiteralMemberName(string member)
    {
        var expression = ParseClass("const Value = class { " + member + " };");
        Assert.Single(expression.Methods.Cast<Stmt>().Concat(expression.Fields));
        Assert.All(expression.Methods, method =>
        {
            Assert.Equal("override", method.Name.Lexeme);
            Assert.False(method.IsOverride);
        });
        Assert.All(expression.Fields, field => Assert.Equal("override", field.Name.Lexeme));
    }

    [Theory]
    [InlineData("class { override value() {} }", "does not extend")]
    [InlineData("class { override get value() { return 1; } }", "does not extend")]
    [InlineData("class { override accessor value = 1; }", "does not extend")]
    [InlineData("class { override value = 1; }", "does not extend")]
    [InlineData("class extends Base { override constructor() {} }", "constructor")]
    [InlineData("class extends Base { override 'constructor'() {} }", "constructor")]
    [InlineData("class extends Base { declare override value: number; }", "'declare'")]
    [InlineData("class extends Base { override declare value: number; }", "'declare'")]
    [InlineData("class extends Base { override static value() {} }", "must precede")]
    [InlineData("class extends Base { static override {} }", "Static blocks")]
    public void InvalidOverrideModifiersAreRejected(string expression, string message)
    {
        var result = new Parser(new Lexer("const Value = " + expression + ";").ScanTokens()).Parse();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains(message, StringComparison.Ordinal));
    }

    private static Expr.ClassExpr ParseClass(string source) => Assert.IsType<Expr.ClassExpr>(
        Assert.IsType<Stmt.Const>(Assert.Single(TestHarness.ParseOrThrow(source))).Initializer);
}
