using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.ParserTests;

public sealed class AssertedAssignmentParserTests
{
    [Theory]
    [InlineData("(target as any) = 2", typeof(Expr.Assign))]
    [InlineData("(<any>target) += 2", typeof(Expr.CompoundAssign))]
    [InlineData("(target satisfies any) ||= 2", typeof(Expr.LogicalAssign))]
    [InlineData("target! = 2", typeof(Expr.Assign))]
    [InlineData("((target as any)!) ??= 2", typeof(Expr.LogicalAssign))]
    public void AssertionsRetainTheirSourceAndUnderlyingReference(string source, Type expected)
    {
        var statement = Assert.IsType<Stmt.Expression>(Assert.Single(TestHarness.ParseOrThrow(source + ";")));
        var assignment = Assert.IsType<Expr.AssertedAssignment>(statement.Expr);
        Assert.IsType(expected, assignment.LoweredAssignment);
        Assert.NotSame(assignment.TargetSyntax, assignment.LoweredAssignment);
    }

    [Theory]
    [InlineData("(obj?.value as any) = 1;")]
    [InlineData("((obj?.value) as any) += 1;")]
    [InlineData("(obj?.[0] satisfies any) ||= 1;")]
    [InlineData("obj?.value! = 1;")]
    [InlineData("(getValue() as any) = 1;")]
    [InlineData("(1 as any) = 1;")]
    public void InvalidReferencesRemainRejected(string source) =>
        Assert.False(new Parser(new Lexer(source).ScanTokens()).Parse().IsSuccess);

    [Fact]
    public void WrittenAssertionTypesSurviveEditorReachability()
    {
        const string source = "// 😀\r\n(target as number) = 2;";
        var document = new SourceDocument("asserted.ts", source);
        var statements = new Parser(new Lexer(source).ScanTokens()).WithSourceDocument(document)
            .WithEditorSyntax().ParseOrThrow();
        var assignment = Assert.IsType<Expr.AssertedAssignment>(Assert.IsType<Stmt.Expression>(Assert.Single(statements)).Expr);
        var grouping = Assert.IsType<Expr.Grouping>(assignment.TargetSyntax);
        var assertion = Assert.IsType<Expr.TypeAssertion>(grouping.Expression);
        var type = Assert.IsType<NamedTypeNode>(assertion.TargetTypeNode);
        var syntax = Assert.IsType<EditorSyntaxIndex>(document.EditorSyntax);
        Assert.Contains(syntax.GetRecords(type), record => record.IsAuthoritative && record.Role == EditorSyntaxRole.Annotation);
        var span = document.Spans.GetSpan(type)!.Value;
        Assert.Equal("number", source[span.Start..span.End]);
        Assert.Equal("(target as number) = 2", Text(assignment));
        Assert.Equal("(target as number) = 2", Text(assignment.LoweredAssignment));

        string Text(object node)
        {
            var range = document.Spans.GetSpan(node)!.Value;
            return source[range.Start..range.End];
        }
    }
}
