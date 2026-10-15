using SharpTS.Parsing;
using Xunit;

namespace SharpTS.Tests.ParserTests;

public sealed class AccessorMemberNameTests
{
    public static IEnumerable<object[]> MethodCases()
    {
        var members = new (string Source, bool Static, AccessModifier Access, bool Async, bool Generator, int Generics)[]
        {
            ("accessor() { return 42; }", false, AccessModifier.Public, false, false, 0),
            ("accessor<T>(value: T): T { return value; }", false, AccessModifier.Public, false, false, 1),
            ("static accessor() { return 42; }", true, AccessModifier.Public, false, false, 0),
            ("private accessor() { return 42; }", false, AccessModifier.Private, false, false, 0),
            ("protected accessor() { return 42; }", false, AccessModifier.Protected, false, false, 0),
            ("async accessor() { return 42; }", false, AccessModifier.Public, true, false, 0),
            ("*accessor() { yield 42; }", false, AccessModifier.Public, false, true, 0),
            ("async *accessor() { yield 42; }", false, AccessModifier.Public, true, true, 0),
            ("static async *accessor() { yield 42; }", true, AccessModifier.Public, true, true, 0),
        };
        foreach (bool expression in new[] { false, true })
            foreach (var member in members)
                yield return [expression, member.Source, member.Static, member.Access,
                    member.Async, member.Generator, member.Generics];
    }

    [Theory, MemberData(nameof(MethodCases))]
    public void AccessorNamedMethodKeepsItsFlagsAndGenericParameters(
        bool expression, string member, bool isStatic, AccessModifier access,
        bool isAsync, bool isGenerator, int genericCount)
    {
        var parsed = ParseMembers(member, expression);
        var method = Assert.Single(parsed.Methods);
        Assert.Equal("accessor", method.Name.Lexeme);
        Assert.Equal(isStatic, method.IsStatic);
        Assert.Equal(access, method.Access);
        Assert.Equal(isAsync, method.IsAsync);
        Assert.Equal(isGenerator, method.IsGenerator);
        Assert.Equal(genericCount, method.TypeParams?.Count ?? 0);
        Assert.Empty(parsed.Fields);
        Assert.Null(parsed.AutoAccessors);
        Assert.Null(parsed.Accessors);
        Assert.NotNull(method.Body);
    }

    public static IEnumerable<object[]> FieldCases()
    {
        foreach (bool expression in new[] { false, true })
            foreach (string member in new[] { "accessor: number;", "accessor = 42;", "accessor;",
                         "accessor?: number;", "accessor!: number;", "accessor", "static readonly accessor = 42;" })
                yield return [expression, member];
    }

    [Theory, MemberData(nameof(FieldCases))]
    public void AccessorNamedFieldUsesOrdinaryFieldContinuation(bool expression, string member)
    {
        var parsed = ParseMembers(member, expression);
        var field = Assert.Single(parsed.Fields);
        Assert.Equal("accessor", field.Name.Lexeme);
        Assert.Equal(member.Contains('?'), field.IsOptional);
        Assert.Equal(member.Contains('!'), field.HasDefiniteAssignmentAssertion);
        Assert.Equal(member.Contains("static", StringComparison.Ordinal), field.IsStatic);
        Assert.Equal(member.Contains("readonly", StringComparison.Ordinal), field.IsReadonly);
        Assert.Equal(member.Contains(':') ? "number" : null, field.TypeAnnotation);
        if (member.Contains('=')) Assert.Equal(42.0, Assert.IsType<double>(Assert.IsType<Expr.Literal>(field.Initializer).Value));
        else Assert.Null(field.Initializer);
        Assert.Empty(parsed.Methods);
        Assert.Null(parsed.AutoAccessors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryMethodAndManualAccessorsDoNotConsumeRealAutoAccessors(bool expression)
    {
        var parsed = ParseMembers("""
            accessor() { return 42; }
            get accessor() { return 43; }
            set accessor(value: number) { }
            accessor value: number = 1;
            static accessor count: number = 2;
            private accessor hidden: number = 3;
            readonly accessor fixed: number = 4;
            """, expression);
        Assert.Equal("accessor", Assert.Single(parsed.Methods).Name.Lexeme);
        Assert.Empty(parsed.Fields);
        Assert.Equal(new[] { "accessor", "accessor" }, parsed.Accessors!.Select(member => member.Name.Lexeme));
        var auto = Assert.IsType<List<Stmt.AutoAccessor>>(parsed.AutoAccessors);
        Assert.Equal(new[] { "value", "count", "hidden", "fixed" }, auto.Select(member => member.Name.Lexeme));
        Assert.All(auto, member => Assert.Equal("number", member.TypeAnnotation));
        Assert.Equal(new[] { false, true, false, false }, auto.Select(member => member.IsStatic));
        Assert.Equal(AccessModifier.Private, auto[2].Access);
        Assert.True(auto[3].IsReadonly);
        Assert.Equal(new[] { 1.0, 2.0, 3.0, 4.0 },
            auto.Select(member => Assert.IsType<double>(Assert.IsType<Expr.Literal>(member.Initializer).Value)));
    }

    [Fact]
    public void AbstractMethodNamedAccessorRemainsAMethodSignature()
    {
        var @class = Assert.IsType<Stmt.Class>(Assert.Single(Parse("abstract class C { abstract accessor(): number; }")));
        var method = Assert.Single(@class.Methods);
        Assert.Equal("accessor", method.Name.Lexeme);
        Assert.True(method.IsAbstract);
        Assert.Null(method.Body);
        Assert.Null(@class.AutoAccessors);
    }

    [Theory]
    [InlineData("class C { accessor #value: number = 0; }", "private identifiers")]
    [InlineData("const C = class { accessor #value: number = 0; };", "private identifiers")]
    [InlineData("class C { *accessor value: number = 0; }", "cannot be generators")]
    [InlineData("const C = class { *accessor value: number = 0; };", "cannot be generators")]
    [InlineData("abstract class C { abstract accessor value: number; }", "cannot be abstract")]
    public void RealAutoAccessorErrorsKeepTheirSpecificDiagnostics(string source, string diagnostic)
    {
        var result = new Parser(new Lexer(source).ScanTokens()).Parse();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, error => error.Message.Contains(diagnostic, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrittenAccessorMethodNameKeepsItsEditorExtent(bool expression)
    {
        string source = Wrap("accessor<T>(value: T): T { return value; }", expression);
        var document = new SourceDocument("accessor-name.ts", source);
        var statements = new Parser(new Lexer(source).ScanTokens())
            .WithSourceDocument(document).WithEditorSyntax().ParseOrThrow();
        var parsed = Members(Assert.Single(statements));
        var method = Assert.Single(parsed.Methods);
        var syntax = Assert.IsType<EditorSyntaxIndex>(document.EditorSyntax);
        var name = Assert.Single(syntax.GetRecords(method), record => record.Role == EditorSyntaxRole.DeclarationName);
        Assert.Equal("accessor", source[name.Span.Start..name.Span.End]);
        Assert.Equal(source.IndexOf("accessor", StringComparison.Ordinal), name.Span.Start);
        Assert.Contains(syntax.GetRecords(method), record => record.Kind == EditorSyntaxKind.ParameterList &&
            source[record.Span.Start..record.Span.End] == "(value: T)");
    }

    private static List<Stmt> Parse(string source) => new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
    private static string Wrap(string member, bool expression) => expression
        ? "const C = class Named { " + member + " };" : "class C { " + member + " }";
    private static ParsedMembers ParseMembers(string member, bool expression) =>
        Members(Assert.Single(Parse(Wrap(member, expression))));
    private static ParsedMembers Members(Stmt statement) => statement switch
    {
        Stmt.Class declaration => new(declaration.Methods, declaration.Fields, declaration.Accessors, declaration.AutoAccessors),
        Stmt.Const { Initializer: Expr.ClassExpr expression } =>
            new(expression.Methods, expression.Fields, expression.Accessors, expression.AutoAccessors),
        _ => throw new InvalidOperationException("Expected a class declaration or expression.")
    };
    private sealed record ParsedMembers(List<Stmt.Function> Methods, List<Stmt.Field> Fields,
        List<Stmt.Accessor>? Accessors, List<Stmt.AutoAccessor>? AutoAccessors);
}
