using SharpTS.Compilation;
using SharpTS.Parsing;
using SharpTS.Tests.SharedTests;
using Xunit;

namespace SharpTS.Tests.Compilation;

public sealed class HandlerLexicalCellIdentityTests
{
    [Fact]
    public void HandlerCapturesRetainDistinctDeclarationTokens()
    {
        var statements = Parse(HandlerLexicalScopeTests.RetainedShadowCatch);
        var analyzer = new ClosureAnalyzer();
        analyzer.Analyze(statements);
        var owner = Assert.Single(statements.OfType<Stmt.Function>());
        var body = owner.Body!;
        var handler = Assert.Single(body.OfType<Stmt.TryCatch>());
        var outerValue = ValueDeclaration(body);
        var tryValue = ValueDeclaration(handler.TryBlock);
        var catchValue = ValueDeclaration(handler.CatchBlock!);
        var finallyValue = ValueDeclaration(handler.FinallyBlock!);
        Assert.NotSame(outerValue, tryValue);
        Assert.NotSame(tryValue, catchValue);
        Assert.NotSame(tryValue, finallyValue);
        Assert.NotSame(catchValue, finallyValue);
        Assert.False(analyzer.IsLoopBodyCellDeclaration(outerValue));

        AssertCapture(analyzer, AssignedArrow(handler.TryBlock, "readTry"), "value", tryValue);
        var catchArrow = AssignedArrow(handler.CatchBlock!, "readCatch");
        AssertCapture(analyzer, catchArrow, "value", catchValue);
        AssertCapture(analyzer, catchArrow, "reason", Assert.IsType<Token>(handler.CatchParam));
        AssertCapture(analyzer, AssignedArrow(handler.FinallyBlock!, "readFinally"), "value", finallyValue);

        var unselected = Parse(HandlerLexicalScopeTests.UncapturedHandlerShadows);
        var unselectedAnalyzer = new ClosureAnalyzer();
        unselectedAnalyzer.Analyze(unselected);
        var unselectedOwner = Assert.Single(unselected.OfType<Stmt.Function>());
        var unselectedBody = unselectedOwner.Body!;
        var unselectedHandler = Assert.Single(unselectedBody.OfType<Stmt.TryCatch>());
        Assert.False(unselectedAnalyzer.IsLoopBodyCellDeclaration(ValueDeclaration(unselectedBody)));
        Assert.False(unselectedAnalyzer.IsLoopBodyCellDeclaration(ValueDeclaration(unselectedHandler.TryBlock)));
        Assert.False(unselectedAnalyzer.IsLoopBodyCellDeclaration(Assert.IsType<Token>(unselectedHandler.CatchParam)));
        Assert.False(unselectedAnalyzer.IsLoopBodyCellDeclaration(ValueDeclaration(unselectedHandler.FinallyBlock!)));

        var namespaceStatements = Parse(HandlerLexicalScopeTests.HandlerVarVisibility);
        var namespaceAnalyzer = new ClosureAnalyzer();
        namespaceAnalyzer.Analyze(namespaceStatements);
        var namespaceRead = Assert.IsType<Expr.ArrowFunction>(Assert.Single(
            namespaceStatements.OfType<Stmt.Const>(), statement => statement.Name.Lexeme == "read").Initializer);
        Assert.DoesNotContain("Number", namespaceAnalyzer.GetCaptures(namespaceRead));
    }

    private static List<Stmt> Parse(string source) =>
        new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();

    private static Token ValueDeclaration(IEnumerable<Stmt> statements) =>
        Assert.Single(statements.OfType<Stmt.Var>(), statement =>
            !statement.IsVar && statement.Name.Lexeme == "value").Name;

    private static Expr.ArrowFunction AssignedArrow(IEnumerable<Stmt> statements, string name) =>
        Assert.IsType<Expr.ArrowFunction>(Assert.Single(statements.OfType<Stmt.Expression>()
            .Select(statement => statement.Expr).OfType<Expr.Assign>(),
            assignment => assignment.Name.Lexeme == name).Value);

    private static void AssertCapture(ClosureAnalyzer analyzer, Expr.ArrowFunction arrow,
        string name, Token declaration)
    {
        Assert.True(analyzer.IsLoopBodyCellDeclaration(declaration));
        Assert.True(analyzer.TryGetLoopBodyCellCapture(arrow, name, out var captured));
        Assert.Same(declaration, captured);
    }
}
