using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class NewTargetContextTests
{
    [Theory]
    [InlineData("class C { method() { return new.target; } }")]
    [InlineData("class C { get value() { return new.target; } }")]
    [InlineData("class C { value = new.target; }")]
    [InlineData("class C { static { const value = new.target; } }")]
    [InlineData("class C { *method() { yield () => new.target; } }")]
    public void JavaScriptFunctionContextsParseButRetainTypeScriptRestrictions(string source)
    {
        var result = new Parser(new Lexer(source).ScanTokens()).Parse();
        Assert.True(result.IsSuccess);
        var diagnostics = new TypeChecker().CheckWithRecovery(result.Statements).Diagnostics;
        Assert.Contains(diagnostics, diagnostic => diagnostic.TsCode == "TS17013");
    }

    [Theory]
    [InlineData("function f(value: any = new.target) { return () => new.target; }")]
    [InlineData("const f = function Named() { return () => new.target; };")]
    [InlineData("class C { constructor() { const arrow = () => new.target; } }")]
    [InlineData("function f() { class C { [new.target ? 'a' : 'b']() {} } }")]
    public void OrdinaryFunctionsConstructorsAndLexicalArrowsAreAccepted(string source)
    {
        var result = new Parser(new Lexer(source).ScanTokens()).Parse();
        Assert.True(result.IsSuccess);
        var diagnostics = new TypeChecker().CheckWithRecovery(result.Statements).Diagnostics;
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.TsCode == "TS17013");
    }
}
