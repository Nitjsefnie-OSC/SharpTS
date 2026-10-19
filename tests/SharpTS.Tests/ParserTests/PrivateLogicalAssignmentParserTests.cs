using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.ParserTests;

public sealed class PrivateLogicalAssignmentParserTests
{
    [Theory]
    [InlineData("||=", TokenType.OR_OR_EQUAL)]
    [InlineData("&&=", TokenType.AND_AND_EQUAL)]
    [InlineData("??=", TokenType.QUESTION_QUESTION_EQUAL)]
    public void LogicalAssignmentsPreserveThePrivateReference(string op, TokenType tokenType)
    {
        foreach (string target in new[] { "other.#value", "((other.#value))" })
        {
            var declaration = Assert.IsType<Stmt.Class>(Assert.Single(TestHarness.ParseOrThrow(
                Source(target + " " + op + " rhs"))));
            var assignment = Assignment(declaration);
            Assert.Equal("other", Assert.IsType<Expr.Variable>(assignment.Object).Name.Lexeme);
            Assert.Equal(TokenType.PRIVATE_IDENTIFIER, assignment.Name.Type);
            Assert.Equal("#value", assignment.Name.Lexeme);
            Assert.Equal(tokenType, assignment.Operator.Type);
            Assert.Equal("rhs", Assert.IsType<Expr.Variable>(assignment.Value).Name.Lexeme);
        }
    }

    [Theory]
    [InlineData("||=")]
    [InlineData("&&=")]
    [InlineData("??=")]
    public void OptionalReceiverChainsRemainInvalidAssignmentTargets(string op)
    {
        foreach (string target in new[] { "other?.nested.#value", "other?.method().#value", "other?.[key].#value" })
        {
            var result = new Parser(new Lexer(Source(target + " " + op + " rhs")).ScanTokens()).Parse();
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Message.Contains("optional", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Theory]
    [InlineData("||=")]
    [InlineData("&&=")]
    [InlineData("??=")]
    public void EditorAssignmentKeepsItsPrivateNameAndReceiver(string op)
    {
        string expression = "other.#value " + op + " rhs";
        string source = Source(expression);
        var parsed = Parser.ParseForEditor(new SourceDocument("private-logical.ts", source), source.Length,
            EditorQueryKind.Syntax, EditorRecoveryPolicy.Default);
        Assert.True(parsed.ParseResult.IsSuccess);
        var assignment = Assignment(Assert.IsType<Stmt.Class>(Assert.Single(parsed.ParseResult.Statements)));
        var member = Assert.Single(parsed.Syntax.Members, member => ReferenceEquals(member.Owner, assignment));
        Assert.True(member.IsPrivate);
        Assert.False(member.IsOptional);
        Assert.Same(assignment.Object, member.Receiver);
        Assert.Equal("#value", source[member.Name.Start..member.Name.End]);
        Assert.Equal(expression, Text(parsed, assignment));
        Assert.Equal("other", Text(parsed, assignment.Object));
        Assert.Equal("rhs", Text(parsed, assignment.Value));
        Assert.Contains(parsed.Syntax.GetRecords(assignment), record =>
            record.Role == EditorSyntaxRole.PrivateName && record.IsAuthoritative &&
            record.Token!.Lexeme == "#value");
    }

    private static string Source(string expression) =>
        "class C { #value = 0; run(other: C) { return " + expression + "; } }";

    private static Expr.LogicalSetPrivate Assignment(Stmt.Class declaration) =>
        Assert.IsType<Expr.LogicalSetPrivate>(Assert.IsType<Stmt.Return>(
            Assert.Single(Assert.Single(declaration.Methods).Body!)).Value);

    private static string Text(EditorParseArtifact parsed, Expr expression)
    {
        Assert.True(parsed.Document.Spans.TryGetSpan(expression, out var span));
        Assert.False(span.IsHidden);
        return parsed.Document.Text[span.Start..span.End];
    }
}
