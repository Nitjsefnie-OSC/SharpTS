using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using SharpTS.Tests.SharedTests;
using SharpTS.TypeSystem;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

/// <summary>
/// Tests that the type checker correctly handles JavaScript var hoisting semantics.
/// Var declarations are hoisted to the top of their enclosing scope, so forward
/// references from within function bodies should not produce false "Undefined variable" errors.
/// </summary>
public class VarHoistingTypeCheckTests
{
    [Fact]
    public void ForwardVarReference_InFunctionBody_Succeeds()
    {
        var source = """
            function f() { return x; }
            var x = 1;
            console.log(f());
            """;

        var output = TestHarness.RunInterpreted(source);
        Assert.Equal("1", output.Trim());
    }

    [Fact]
    public void ForwardVarReference_InFunctionExpression_Succeeds()
    {
        var source = """
            var getIt = function() { return _val; };
            var _val = 42;
            console.log(getIt());
            """;

        var output = TestHarness.RunInterpreted(source);
        Assert.Equal("42", output.Trim());
    }

    [Fact]
    public void ForwardVarReference_InArrowFunction_Succeeds()
    {
        var source = """
            var getIt = () => _val;
            var _val = 42;
            console.log(getIt());
            """;

        var output = TestHarness.RunInterpreted(source);
        Assert.Equal("42", output.Trim());
    }

    [Fact]
    public void ForwardVarReference_InObjectLiteralGetter_Succeeds()
    {
        // Simulates the Babel CJS interop pattern used by uuid and other packages
        var source = """
            var obj = {
                get value() { return _foo; }
            };
            var _foo = "bar";
            console.log(obj.value);
            """;

        var output = TestHarness.RunInterpreted(source);
        Assert.Equal("bar", output.Trim());
    }

    [Fact]
    public void ForwardVarReference_DuplicateDeclarations_Succeeds()
    {
        var source = """
            function f() { return x; }
            var x = 1;
            var x = 2;
            console.log(f());
            """;

        var output = TestHarness.RunInterpreted(source);
        Assert.Equal("2", output.Trim());
    }

    // #533: a function declared BEFORE a let/const may reference that binding in its body — the
    // body only runs once the binding is initialized, so TypeScript (which collects every lexical
    // binding in a scope before checking bodies) accepts it. SharpTS now hoists let/const the same
    // way it hoists var and class. (A *direct* use-before-declaration is still rejected — at runtime
    // — because the binding isn't created until its statement executes; see the StillFails tests.)
    [Fact]
    public void ForwardLetReference_InFunctionBody_Succeeds()
    {
        var source = """
            function f() { return y; }
            let y = 1;
            console.log(f());
            """;

        var output = TestHarness.RunInterpreted(source);
        Assert.Equal("1", output.Trim());
    }

    [Fact]
    public void ForwardConstReference_InFunctionBody_Succeeds()
    {
        var source = """
            function f() { return z; }
            const z = 5;
            console.log(f());
            """;

        var output = TestHarness.RunInterpreted(source);
        Assert.Equal("5", output.Trim());
    }

    [Fact]
    public void BlockForwardReference_KeepsInnerDeclarationIdentity()
    {
        var document = new SourceDocument("block-shadow.ts", ForOfBodyLexicalCellTests.AnnotatedShadowSource);
        var statements = new Parser(new Lexer(document.Text).ScanTokens())
            .WithSourceDocument(document).ParseOrThrow();
        var checker = new TypeChecker();
        checker.Check(statements, document);

        var outer = Assert.IsType<Stmt.Var>(statements[0]);
        var block = Assert.IsType<Stmt.Block>(statements[1]);
        var read = Assert.IsType<Stmt.Const>(block.Statements[0]);
        var forward = Assert.IsType<Expr.Variable>(Assert.IsType<Expr.ArrowFunction>(read.Initializer).ExpressionBody);
        var inner = Assert.IsType<Stmt.Var>(block.Statements[1]);
        var outsideCall = Assert.IsType<Expr.Call>(Assert.IsType<Stmt.Expression>(statements[2]).Expr);
        var outside = Assert.IsType<Expr.Variable>(Assert.Single(outsideCall.Arguments));

        Assert.Same(inner.Name, Assert.Single(checker.Bindings.FindDefinitions(document, forward.Name.Start)).Name);
        Assert.Same(outer.Name, Assert.Single(checker.Bindings.FindDefinitions(document, outside.Name.Start)).Name);
        Assert.NotEqual(
            Assert.Single(checker.Bindings.FindSymbols(document, forward.Name.Start)).Id,
            Assert.Single(checker.Bindings.FindSymbols(document, outside.Name.Start)).Id);
    }

    [Theory]
    [InlineData("let value = 1; const wrong: string = value;")]
    [InlineData("const value = 'inner'; const wrong: number = value;")]
    public void BlockForwardReference_LaterUsesRetainPreciseTypes(string declarations)
    {
        var source = "{ const read = () => value; " + declarations + " }";
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();

        var error = Assert.Throws<TypeMismatchException>(() => new TypeChecker().Check(statements));

        Assert.Equal("TS2322", error.Diagnostic.TsCode);
    }

    [Fact]
    public void DirectLetReference_BeforeDeclaration_StillFailsAtRuntime()
    {
        // Not deferred behind a function: the read executes before `let x` runs, so the binding
        // does not exist yet. This stays an error (now surfaced at runtime, matching how a forward
        // class reference behaves), never silently producing undefined.
        var source = """
            console.log(x);
            let x = 1;
            """;

        var ex = Assert.ThrowsAny<Exception>(() => TestHarness.RunInterpreted(source));
        Assert.Contains("Undefined variable", ex.Message);
    }
}
