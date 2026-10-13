using SharpTS.Compilation;
using SharpTS.Parsing;
using SharpTS.Tests.SharedTests;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public sealed class ConstructorParameterCaptureTests
{
    [Fact]
    public void ConstructorParameterCapturesStayBoxedWhileLocalAndOrdinaryFunctionCapturesStayNumeric()
    {
        var source = ConstructorNullArgumentTests.CaptureSource + "\n" + ConstructorNullArgumentTests.OrdinaryCaptureSource
            + "\n" + ConstructorNullArgumentTests.ExpressionCaptureProofSource;
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var types = new TypeChecker().Check(statements);
        var compiler = new ILCompiler("constructor_capture");
        compiler.Compile(statements, types);

        var declaration = statements.OfType<Stmt.Class>().Single();
        var constructor = declaration.Methods.Single(method => method.Name.Lexeme == "constructor");
        Assert.False(types.IsStableNumericFunctionCaptureField(constructor, "value"));
        Assert.True(types.IsStableNumericFunctionCaptureField(constructor, "stable"));
        var expression = (Expr.ClassExpr)statements.OfType<Stmt.Const>().Single(statement => statement.Name.Lexeme == "E").Initializer;
        var expressionConstructor = expression.Methods.Single(method => method.Name.Lexeme == "constructor");
        Assert.False(types.IsStableNumericFunctionCaptureField(expressionConstructor, "value"));
        Assert.True(types.IsStableNumericFunctionCaptureField(expressionConstructor, "stable"));
        var function = statements.OfType<Stmt.Function>().Single(statement => statement.Name.Lexeme == "constructor");
        Assert.True(types.IsStableNumericFunctionCaptureField(function, "value"));
        Assert.True(types.IsStableNumericFunctionCaptureField(function, "stable"));
    }
}
