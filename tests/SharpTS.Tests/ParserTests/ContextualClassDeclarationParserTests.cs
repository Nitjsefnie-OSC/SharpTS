using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.ParserTests;

public sealed class ContextualClassDeclarationParserTests
{
    public static IEnumerable<object[]> ValidNames() => new[]
    {
        "abstract", "accessor", "as", "asserts", "async", "constructor", "declare", "from", "get",
        "global", "infer", "is", "keyof", "module", "namespace", "of", "out", "override", "readonly",
        "satisfies", "set", "type", "unique", "using", "Symbol", "BigInt",
    }.Select(name => new object[] { name });

    [Theory, MemberData(nameof(ValidNames))]
    public void ContextualNamesProduceIdentifierBindings(string name)
    {
        string source = "\r\nclass " + name + " { static create() {} instanceMethod() {} }";
        var tokens = new Lexer(source).ScanTokens();
        Token written = tokens[1];
        var declaration = Assert.IsType<Stmt.Class>(Assert.Single(new Parser(tokens).ParseOrThrow()));
        Assert.Equal(TokenType.IDENTIFIER, declaration.Name.Type);
        Assert.Equal(name, declaration.Name.Lexeme);
        Assert.Equal(written.Line, declaration.Name.Line);
        Assert.Equal(written.Start, declaration.Name.Start);
        Assert.Equal(written.SourceOrderOffset, declaration.Name.SourceOrderOffset);
        Assert.Same(written.SourceIdentity, declaration.Name.SourceIdentity);
        Assert.Same(written, tokens[1]);
    }

    [Theory]
    [InlineData("abstract class type {}", true, false)]
    [InlineData("declare class type {}", false, true)]
    [InlineData("declare abstract class type {}", true, true)]
    public void ModifiedDeclarationsUseTheSameBindingRules(string source, bool isAbstract, bool isDeclare)
    {
        var declaration = Assert.IsType<Stmt.Class>(Assert.Single(TestHarness.ParseOrThrow(source)));
        Assert.Equal("type", declaration.Name.Lexeme);
        Assert.Equal(isAbstract, declaration.IsAbstract);
        Assert.Equal(isDeclare, declaration.IsDeclare);
    }

    [Fact]
    public void ExportedContextualClassKeepsItsDeclaration()
    {
        var export = Assert.IsType<Stmt.Export>(Assert.Single(TestHarness.ParseOrThrow("export class type {}")));
        Assert.Equal("type", Assert.IsType<Stmt.Class>(export.Declaration).Name.Lexeme);
    }

    [Fact]
    public void ComputedAccessOnUsingNamedClassRemainsAnExpression()
    {
        var statements = TestHarness.ParseOrThrow("class using {} using[\"value\"]();");
        var statement = Assert.IsType<Stmt.Expression>(statements[1]);
        var call = Assert.IsType<Expr.Call>(statement.Expr);
        var access = Assert.IsType<Expr.GetIndex>(call.Callee);
        Assert.Equal("using", Assert.IsType<Expr.Variable>(access.Object).Name.Lexeme);
    }

    [Theory]
    [InlineData("type")]
    [InlineData("readonly")]
    [InlineData("as")]
    [InlineData("using")]
    public void EditorDeclarationNameKeepsItsWrittenTokenAndRange(string name)
    {
        string source = "// 😀\r\nclass " + name + " { static create() {} }";
        var parsed = Parser.ParseForEditor(new SourceDocument("class-name.ts", source), source.Length,
            EditorQueryKind.Syntax, EditorRecoveryPolicy.Default);
        Assert.True(parsed.ParseResult.IsSuccess);
        var declaration = Assert.IsType<Stmt.Class>(Assert.Single(parsed.ParseResult.Statements));
        int start = source.IndexOf("class ", StringComparison.Ordinal) + "class ".Length;
        Token written = Assert.Single(parsed.Tokens, token => token.Start == start);
        Assert.Equal(new SourceSpan(start, start + name.Length), declaration.Name.Span);
        Assert.Same(written.SourceIdentity, declaration.Name.SourceIdentity);
        Assert.Contains(parsed.Syntax.GetRecords(declaration), record =>
            record.Role == EditorSyntaxRole.DeclarationName && record.IsAuthoritative &&
            ReferenceEquals(record.Token, written) && record.Span == declaration.Name.Span);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("return")]
    [InlineData("function")]
    [InlineData("const")]
    [InlineData("extends")]
    [InlineData("implements")]
    [InlineData("interface")]
    [InlineData("private")]
    [InlineData("protected")]
    [InlineData("public")]
    [InlineData("static")]
    [InlineData("let")]
    [InlineData("yield")]
    [InlineData("string")]
    [InlineData("number")]
    [InlineData("boolean")]
    [InlineData("symbol")]
    [InlineData("bigint")]
    [InlineData("unknown")]
    [InlineData("never")]
    [InlineData("undefined")]
    public void ReservedClassNamesRemainRejected(string name) =>
        Assert.False(new Parser(new Lexer("class " + name + " {}").ScanTokens()).Parse().IsSuccess);
}
