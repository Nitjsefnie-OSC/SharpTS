using SharpTS.Compilation;
using SharpTS.Compilation.Emitters;
using SharpTS.Parsing;
using SharpTS.Parsing.Visitors;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public sealed class ForwardedIntrinsicAnalysisTests
{
    [Theory]
    [InlineData("parseInt(input, 10)", false)]
    [InlineData("Number.parseInt(input, 10)", false)]
    [InlineData("Math.min()", false)]
    [InlineData("Math.max(position, 2)", false)]
    [InlineData("input.indexOf(needle, position)", false)]
    [InlineData("input.includes(needle)", false)]
    [InlineData("parseInt?.(input, 10)", true)]
    [InlineData("Number?.parseInt(input, 10)", true)]
    [InlineData("parseInt(...values)", true)]
    [InlineData("parseInt(input, 16)", true)]
    [InlineData("Math.min(...values)", true)]
    [InlineData("Math.min(unknown)", true)]
    [InlineData("Math.abs(position)", true)]
    [InlineData("input?.indexOf(needle)", true)]
    [InlineData("input.includes?.(needle)", true)]
    [InlineData("input.indexOf(...values)", true)]
    [InlineData("input.indexOf()", true)]
    [InlineData("input.indexOf(needle, position, 2)", true)]
    [InlineData("unknown.indexOf(needle)", true)]
    [InlineData("makeString().indexOf(needle)", true)]
    [InlineData("input.indexOf(makeString())", true)]
    [InlineData("input.indexOf(needle, unknown)", true)]
    public void OnlyExactLoweredIntrinsicShapesHaveNativeReturns(string expression, bool raw)
    {
        var statements = Parse($"function forward() {{ return {expression}; }}");
        var map = new TypeMap();
        // Supply checked primitive call results even for optional/spread shapes. The
        // proof must independently reject them, without relying on checker narrowing.
        new CheckedPrimitiveTypes(map).Visit(statements[0]);
        var features = new RuntimeFeatureDetector().Detect(statements, map);
        ForwardedReturnValueAnalyzer.Analyze(statements, map, features, null);
        Assert.Equal(raw, ReturnSlotAnalysis.BlockReturnsMayBeUndefined(((Stmt.Function)statements[0]).Body, map));
    }

    [Theory]
    [InlineData("function nested(parseInt: any) {}")]
    [InlineData("const nested = function parseInt() {};")]
    [InlineData("class Holder { set value(parseInt: any) {} }")]
    [InlineData("const holder = { set value(parseInt: any) {} };")]
    [InlineData("class Holder { static value = ((parseInt: any) => 0)(0); }")]
    [InlineData("class Holder { static accessor value = ((parseInt: any) => 0)(0); }")]
    [InlineData("class Holder { static { const parseInt = 0; } }")]
    [InlineData("using parseInt = resource;")]
    [InlineData("import { value as parseInt } from './mock';")]
    [InlineData("try {} catch (parseInt) {}")]
    public void AnyModuleBindingConservativelyRefusesCanonicalParseInt(string binding)
    {
        AssertRawReturn(binding, expectedRaw: true);
    }

    [Theory]
    [InlineData("class Holder { parseInt() { return 1; } Number() {} Math() {} }")]
    [InlineData("const holder = { parseInt() { return 1; } };")]
    public void PropertyNamesDoNotShadowLexicalBuiltins(string declarations)
    {
        AssertRawReturn(declarations, expectedRaw: false);
    }

    [Fact]
    public void MissingFeatureProofKeepsObjectReturn()
    {
        var statements = Parse("function forward() { return parseInt(input, 10); }");
        var map = new TypeMap();
        new CheckedPrimitiveTypes(map).Visit(statements[0]);
        ForwardedReturnValueAnalyzer.Analyze(statements, map, null, null);
        Assert.True(ReturnSlotAnalysis.BlockReturnsMayBeUndefined(((Stmt.Function)statements[0]).Body, map));
    }

    [Fact]
    public void DecimalParseIntShapeRejectsSpreadIndependentlyOfItsCheckedType()
    {
        var statements = Parse("function forward() { return parseInt(...values, 10); }");
        var call = (Expr.Call)((Stmt.Return)((Stmt.Function)statements[0]).Body![0]).Value!;
        var map = new TypeMap();
        map.Set(call.Arguments[0], new TypeInfo.String());
        Assert.False(NumberStaticEmitter.IsDecimalStringParseInt(map, call.Arguments));
    }

    private static void AssertRawReturn(string declarations, bool expectedRaw)
    {
        var statements = Parse("function forward() { return parseInt(input, 10); } " + declarations);
        var map = new TypeMap();
        var types = new CheckedPrimitiveTypes(map);
        foreach (var statement in statements) types.Visit(statement);
        ForwardedReturnValueAnalyzer.Analyze(statements, map, new RuntimeFeatureDetector().Detect(statements, map), null);
        Assert.Equal(expectedRaw, ReturnSlotAnalysis.BlockReturnsMayBeUndefined(((Stmt.Function)statements[0]).Body, map));
    }

    private static List<Stmt> Parse(string source) =>
        new Parser(new Lexer(source).ScanTokens(), DecoratorMode.Stage3).ParseOrThrow();

    private sealed class CheckedPrimitiveTypes(TypeMap map) : AstVisitorBase
    {
        protected override void VisitCall(Expr.Call call)
        {
            base.VisitCall(call);
            map.Set(call, call.Callee is Expr.Get { Name.Lexeme: "includes" }
                ? TypeInfo.Primitive.Boolean : TypeInfo.Primitive.Number);
        }
        protected override void VisitVariable(Expr.Variable variable) => map.Set(variable,
            variable.Name.Lexeme is "input" or "needle" ? new TypeInfo.String() :
            variable.Name.Lexeme == "position" ? TypeInfo.Primitive.Number : new TypeInfo.Any());
        protected override void VisitLiteral(Expr.Literal literal) => map.Set(literal,
            literal.Value is string ? new TypeInfo.String() : TypeInfo.Primitive.Number);
    }
}
