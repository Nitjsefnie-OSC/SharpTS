using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class PrivateCompoundAssignmentTypeTests
{
    [Theory]
    [InlineData("class C { readonly #x:number=1; constructor(){this.#x += 1;} read(){return this.#x;} } console.log(new C().read());", "2\n")]
    [InlineData("class C { #x:number=1; change(){const result:number=(this.#x += 2); return result;} } console.log(new C().change());", "3\n")]
    [InlineData("class C { #x:string='a'; change(){const result:string=(this.#x += 'b'); return result;} } console.log(new C().change());", "ab\n")]
    [InlineData("class C { #x:number=1; change(value:any){const result:string=(this.#x += value); return typeof result;} } console.log(new C().change(2));", "number\n")]
    [InlineData("class C { #x:number=1; change<T extends C>(other:T){return other.#x += 1;} } class D extends C { #x=7; } console.log(new C().change(new D()));", "2\n")]
    [InlineData("class C { static #x:number=1; static change(other:any){return other.#x += 1;} } console.log(C.change(C));", "2\n")]
    [InlineData("class C { #x:number=1; change(){function use<T extends C>(other:T){return other.#x += 1;} return use(this);} } console.log(new C().change());", "2\n")]
    public void CompoundResultsHaveTheFieldOperationType(string source, string expected)
        => Assert.Equal(expected, TestHarness.RunInterpreted(source));

    public static IEnumerable<object[]> Rejections()
    {
        yield return ["readonly method", "TS2540", "class C { readonly #x:number=1; change(){this.#x += 1;} }"];
        yield return ["readonly constructor alias", "TS2540", "class C { readonly #x:number=1; constructor(){const other=this; other.#x += 1;} }"];
        yield return ["readonly constructor arrow", "TS2540", "class C { readonly #x:number=1; constructor(){const change=()=>this.#x += 1;} }"];
        yield return ["readonly static block", "TS2540", "class C { static readonly #x:number=1; static {this.#x += 1;} }"];
        yield return ["readonly static method", "TS2540", "class C { static readonly #x:number=1; static change(){this.#x += 1;} }"];
        yield return ["private method", "TS2803", "class C { #method() {} change(){this.#method += 1;} }"];
        yield return ["numeric result", "TS2322", "class C { #x:number=1; change(){const result:string=(this.#x += 1);} }"];
        yield return ["string result", "TS2322", "class C { #x:string='x'; change(){const result:number=(this.#x += 'y');} }"];
        yield return ["write compatibility", "TS2322", "class C { #x:number=1; change(){this.#x += 'text';} }"];
        yield return ["different instance private declaration", "TS18013", "class Other { #x=1; } class C { #x=1; change(other:Other){return other.#x += 1;} }"];
        yield return ["missing instance private declaration", "TS2339", "class Other {} class C { #x=1; change(other:Other){return other.#x += 1;} }"];
        yield return ["unknown receiver", "TS18046", "class C { #x=1; change(other:unknown){return other.#x += 1;} }"];
        yield return ["different static private declaration", "TS18013", "class Other { static #x=1; } class C { static #x=1; static change(){return Other.#x += 1;} }"];
        yield return ["instance target on constructor", "TS2339", "class C { #x=1; change(){return C.#x += 1;} }"];
        yield return ["static target on instance", "TS2339", "class C { static #x=1; change(){return this.#x += 1;} }"];
        yield return ["static target on known derived constructor", "TS2339", "class C { static #x=1; static change(){const D=class extends C {}; return D.#x += 1;} }"];
        yield return ["static target with foreign private ancestor", "TS2339", "class Foreign { static #x=7; } class Other extends Foreign {} class C { static #x=1; static change(){return Other.#x += 1;} }"];
        yield return ["unconstrained receiver", "TS2339", "class C { #x=1; change(){function use<T extends any>(other:T){return other.#x += 1;} return use;} }"];
    }

    [Theory, MemberData(nameof(Rejections))]
    public void InvalidPrivateCompoundWritesHaveSpecificDiagnostics(string name, string code, string source)
    {
        Assert.NotEmpty(name);
        var error = Assert.ThrowsAny<TypeCheckException>(() => TestHarness.RunInterpreted(source));
        Assert.Equal(code, error.Diagnostic.TsCode);
    }
}
