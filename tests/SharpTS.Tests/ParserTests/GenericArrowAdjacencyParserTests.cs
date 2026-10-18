using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.ParserTests;

public sealed class GenericArrowAdjacencyParserTests
{
    public static IEnumerable<object[]> ArrowCases()
    {
        foreach (int depth in new[] { 1, 2, 3, 4, 6 })
        {
            string returnType = "number";
            for (int level = 0; level < depth; level++) returnType = "Box<" + returnType + ">";
            foreach (bool isAsync in new[] { false, true })
                foreach (bool isGeneric in new[] { false, true })
                {
                    string prefix = (isAsync ? "async " : "") + (isGeneric ? "<T>" : "");
                    yield return [$"const run = {prefix}(value: any): {returnType}=> value;",
                        returnType, isAsync, isGeneric];
                }
        }
    }

    [Theory, MemberData(nameof(ArrowCases))]
    public void AdjacentAndSpacedGenericReturnsHaveTheSameArrowShape(
        string source, string returnType, bool isAsync, bool isGeneric)
    {
        foreach (string spelling in new[] { source, source.Replace(">=>", "> =>", StringComparison.Ordinal) })
        {
            var arrow = Assert.IsType<Expr.ArrowFunction>(
                Assert.IsType<Stmt.Const>(Assert.Single(TestHarness.ParseOrThrow(spelling))).Initializer);
            Assert.Equal(returnType, arrow.ReturnType);
            Assert.Equal(isAsync, arrow.IsAsync);
            Assert.Equal(isGeneric, arrow.TypeParams is { Count: 1 });
            Assert.Equal("value", Assert.IsType<Expr.Variable>(arrow.ExpressionBody).Name.Lexeme);
        }
    }

    [Theory, MemberData(nameof(ArrowCases))]
    public void EditorTokensAndTypeRangesKeepTheirWrittenOffsets(
        string source, string returnType, bool isAsync, bool isGeneric)
    {
        var parsed = ParseEditor(source);
        Assert.True(parsed.ParseResult.IsSuccess);
        var arrow = Assert.IsType<Expr.ArrowFunction>(
            Assert.IsType<Stmt.Const>(Assert.Single(parsed.ParseResult.Statements)).Initializer);
        Assert.Equal(isAsync, arrow.IsAsync);
        Assert.Equal(isGeneric, arrow.TypeParams is { Count: 1 });
        Assert.NotNull(arrow.ReturnTypeNode);
        Assert.Equal(returnType, Text(parsed, arrow.ReturnTypeNode!));
        var arrowToken = Assert.Single(parsed.Tokens, token => token.Type == TokenType.ARROW);
        Assert.Equal("=>", source[arrowToken.Start..arrowToken.End]);
        Assert.Same(Assert.IsType<Expr.Variable>(arrow.ExpressionBody).Name.SourceIdentity,
            arrowToken.SourceIdentity);
        Assert.All(parsed.Tokens.Where(token => token.Type != TokenType.EOF), token =>
            Assert.Equal(token.Lexeme, source[token.Start..token.End]));
        Assert.Contains(parsed.Syntax.GetRecords(arrow), record => record.Role == EditorSyntaxRole.Header &&
            record.IsAuthoritative && record.Span.End == arrowToken.End);
        Assert.Equal("value", Text(parsed, arrow.ExpressionBody!));
    }

    [Theory]
    [InlineData("const result = a < b >= c;")]
    [InlineData("const result = a < b < c >> d;")]
    [InlineData("const result = a < b < c >>> d;")]
    [InlineData("target >>= 1;")]
    [InlineData("target >>>= 1;")]
    public void OrdinaryOperatorsSurviveFailedTypeArgumentSpeculation(string source)
    {
        var tokens = new Lexer(source).ScanTokens();
        var original = tokens.ToArray();
        Assert.True(new Parser(tokens).Parse().IsSuccess);
        Assert.Equal(original.Length, tokens.Count);
        for (int index = 0; index < tokens.Count; index++) Assert.Same(original[index], tokens[index]);
    }

    [Theory]
    [InlineData("const result = a < b >>= c;")]
    [InlineData("const result = a < b >>>= c;")]
    [InlineData("const result = a < b>=> c;")]
    public void RejectedSpeculationRestoresCompoundAndFollowingTokens(string source)
    {
        var tokens = new Lexer(source).ScanTokens();
        var original = tokens.ToArray();
        Assert.False(new Parser(tokens).Parse().IsSuccess);
        for (int index = 0; index < tokens.Count; index++) Assert.Same(original[index], tokens[index]);
    }

    [Fact]
    public void OuterArrowSpeculationCanRewindAnInnerReconstructedArrow()
    {
        const string source = "let callback: any; const grouped = (callback = async (): Promise<number>=> 47);";
        var parsed = ParseEditor(source);
        Assert.True(parsed.ParseResult.IsSuccess);
        var grouped = Assert.IsType<Expr.Grouping>(parsed.ParseResult.Statements.OfType<Stmt.Const>().Single().Initializer);
        var arrow = Assert.IsType<Expr.ArrowFunction>(Assert.IsType<Expr.Assign>(grouped.Expression).Value);
        Assert.Equal("Promise<number>", arrow.ReturnType);
        Assert.Equal("Promise<number>", Text(parsed, arrow.ReturnTypeNode!));
        Assert.Contains(parsed.Syntax.GetRecords(arrow), record => record.Role == EditorSyntaxRole.Header &&
            source[record.Span.Start..record.Span.End] == "async (): Promise<number>=>");
    }

    [Theory]
    [InlineData("const run = (): Box<number>= >value;")]
    [InlineData("const run = (): Box<number>=/* comment */>value;")]
    [InlineData("const run = (): Box<number>=\n>value;")]
    public void SeparatedEqualsAndGreaterDoNotBecomeAnArrow(string source) =>
        Assert.False(new Parser(new Lexer(source).ScanTokens()).Parse().IsSuccess);

    [Theory]
    [InlineData("Box<number>")]
    [InlineData("Box<Box<number>>")]
    [InlineData("Box<Box<Box<Box<number>>>>")]
    public void AdjacentGenericInitializerKeepsItsAssignment(string type)
    {
        var variable = Assert.IsType<Stmt.Var>(Assert.Single(TestHarness.ParseOrThrow($"let item: {type}=value;")));
        Assert.Equal(type, variable.TypeAnnotation);
        Assert.Equal("value", Assert.IsType<Expr.Variable>(variable.Initializer).Name.Lexeme);
    }

    [Fact]
    public void GenericConstraintCanTouchItsDefaultEquals()
    {
        var function = Assert.IsType<Stmt.Function>(Assert.Single(TestHarness.ParseOrThrow(
            "function identity<T extends Box<number>=Box<number>>(value: T): T { return value; }")));
        var parameter = Assert.Single(function.TypeParams!);
        Assert.Equal("Box<number>", parameter.Constraint);
        Assert.Equal("Box<number>", parameter.Default);
    }

    [Fact]
    public void GenericReturnInsideAFunctionTypeKeepsTheOuterArrow()
    {
        var arrow = Assert.IsType<Expr.ArrowFunction>(Assert.IsType<Stmt.Const>(
            Assert.Single(TestHarness.ParseOrThrow("const factory = (): () => Array<number>=> () => [];"))).Initializer);
        Assert.Equal("() => Array<number>", arrow.ReturnType);
        Assert.IsType<Expr.ArrowFunction>(arrow.ExpressionBody);
    }

    private static EditorParseArtifact ParseEditor(string source) => Parser.ParseForEditor(
        new SourceDocument("generic-arrow.ts", source), source.Length, EditorQueryKind.Syntax, EditorRecoveryPolicy.Default);

    private static string Text(EditorParseArtifact parsed, object node)
    {
        Assert.True(parsed.Document.Spans.TryGetSpan(node, out var span));
        Assert.False(span.IsHidden);
        return parsed.Document.Text[span.Start..span.End];
    }
}
