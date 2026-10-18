using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.ParserTests;

public sealed class SuperAssignmentParserTests
{
    [Theory]
    [InlineData("super.value = next + 1;")]
    [InlineData("(super.value) = next + 1;")]
    public void SimpleAssignmentRetainsSuperReferenceAndRightHandSide(string source)
    {
        var statement = Assert.IsType<Stmt.Expression>(Assert.Single(TestHarness.ParseOrThrow(source)));
        var assignment = Assert.IsType<Expr.SetSuper>(statement.Expr);
        Assert.Equal(TokenType.SUPER, assignment.Target.Keyword.Type);
        Assert.Equal("value", assignment.Target.Method!.Lexeme);
        var value = Assert.IsType<Expr.Binary>(assignment.Value);
        Assert.Equal(TokenType.PLUS, value.Operator.Type);
        Assert.Equal("next", Assert.IsType<Expr.Variable>(value.Left).Name.Lexeme);
        Assert.Equal(1d, Assert.IsType<Expr.Literal>(value.Right).Value);
    }

    [Theory]
    [InlineData("holder?.value = next;")]
    [InlineData("super.value?.nested = next;")]
    [InlineData("super.value?.[key] = next;")]
    public void OptionalChainAssignmentTargetsRemainRejected(string source)
    {
        var result = new Parser(new Lexer(source).ScanTokens()).Parse();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("optional", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SuperConstructorCallCannotBeAnAssignmentTarget()
    {
        var result = new Parser(new Lexer("super() = next;").ScanTokens()).Parse();
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("Invalid assignment target"));
    }

    [Fact]
    public void EditorAssignmentRetainsSuperTargetAndMemberSyntax()
    {
        const string source = "super.value = next + 1;";
        var parsed = Parser.ParseForEditor(new SourceDocument("super-assignment.ts", source), source.Length,
            EditorQueryKind.Syntax, EditorRecoveryPolicy.Default);
        Assert.True(parsed.ParseResult.IsSuccess);
        var assignment = Assert.IsType<Expr.SetSuper>(
            Assert.IsType<Stmt.Expression>(Assert.Single(parsed.ParseResult.Statements)).Expr);

        Assert.Equal("super.value = next + 1", Text(parsed, assignment));
        Assert.Equal("super.value", Text(parsed, assignment.Target));
        Assert.Equal("next + 1", Text(parsed, assignment.Value));
        var member = Assert.Single(parsed.Syntax.Members, member => ReferenceEquals(member.Owner, assignment));
        Assert.Same(assignment.Target, member.Receiver);
        Assert.Equal("value", member.Name.Lexeme);
        Assert.Equal(new SourceSpan(0, 11), member.Span);
        Assert.Equal(new SourceSpan(5, 6), member.OperatorSpan);
        Assert.False(member.IsOptional);
        Assert.Contains(parsed.Syntax.GetRecords(assignment), record =>
            record.Role == EditorSyntaxRole.MemberName && record.IsAuthoritative &&
            record.Span == new SourceSpan(6, 11) && record.Token!.Lexeme == "value");
        Assert.Contains(parsed.Syntax.GetRecords(assignment.Target), record =>
            record.Kind == EditorSyntaxKind.Expression && record.IsAuthoritative);
    }

    private static string Text(EditorParseArtifact parsed, Expr expression)
    {
        Assert.True(parsed.Document.Spans.TryGetSpan(expression, out SourceSpan span));
        Assert.False(span.IsHidden);
        return parsed.Document.Text[span.Start..span.End];
    }
}
