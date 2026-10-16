using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrivateTypedSpreadTests
{
    [Theory, ModeData]
    public void TypedPrivateRestMethodsAcceptArraysTuplesAndIterables(ExecutionMode mode)
    {
        const string source = """
            function* values() { yield 5; yield 6; }
            class C {
                static #m(...args: number[]) { return args.join(","); }
                static read() {
                    console.log(this.#m(...[1, 2]));
                    console.log(this.#m(...[3, 4] as const));
                    console.log(this.#m(...new Set<number>([3, 4]), ...values(), 7));
                    console.log(this.#m());
                }
            }
            C.read();
            """;
        Assert.Equal("1,2\n3,4\n3,4,5,6,7\n\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void TuplesConsumeRegularParametersBeforePrivateRestElements(ExecutionMode mode)
    {
        const string source = """
            class C {
                static #m(first: string, ...args: number[]) { return first + ":" + args.join(","); }
                static #field: (first: string, ...args: number[]) => string =
                    (first: string, ...args: number[]) => first + ":" + args.join(",");
                static read() {
                    console.log(this.#m(...["tuple", 1, 2] as const));
                    console.log(this.#m("array", ...[3, 4], 5));
                    console.log(this.#field(...["field", 6, 7] as const));
                }
            }
            C.read();
            """;
        Assert.Equal("tuple:1,2\narray:3,4,5\nfield:6,7\n", TestHarness.Run(source, mode));
    }

    // Direct instance private rest emission has its own runtime follow-up (#2153).
    [Theory]
    [InlineData("#m(...args: number[]) { return args.join(','); }", "this.#m(...[1, 2])")]
    [InlineData("#m: (...args: number[]) => string = (...args: number[]) => args.join(',');", "this.#m(...[1, 2])")]
    [InlineData("#m(first: number, second?: string) {}", "this.#m(...[1, 'two'] as const)")]
    [InlineData("#m(first: number, second?: string) {}", "this.#m(...[1] as const)")]
    [InlineData("#m(first: number, ...tail: number[]) {}", "this.#m(...([1, 2] as [number, ...number[]]))")]
    [InlineData("#m(first: number, second: string) {}", "this.#m(...([] as any as [...[number, string]]))")]
    [InlineData("#m(first: number, second: string) {}", "this.#m(...([] as any as [...[...[number, string]]]))")]
    [InlineData("#m(first: number, second?: string) {}", "this.#m(...([] as any as [number, string?]))")]
    public void InstancePrivateSignaturesAcceptTypedSpreadCalls(string declaration, string call)
    {
        var source = $"class C {{ {declaration} read() {{ {call}; }} }}";
        new TypeChecker().Check(TestHarness.ParseOrThrow(source));
    }

    [Theory]
    [InlineData("...['bad']", "TS2345")]
    [InlineData("...[1, 'bad'] as const", "TS2345")]
    [InlineData("...new Set<string>(['bad'])", "TS2345")]
    [InlineData("...42", "TS2488")]
    [InlineData("...[1], 'bad'", "TS2345")]
    [InlineData("...[1], ...['bad']", "TS2345")]
    [InlineData("...[1], ...42", "TS2488")]
    [InlineData("...([] as any as [number, ...string[]])", "TS2345")]
    public void EveryRestArgumentAndSpreadIsChecked(string arguments, string code)
    {
        var source = $$"""
            class C {
                static #m(...args: number[]) {}
                static read() { this.#m({{arguments}}); }
            }
            """;
        var error = Assert.ThrowsAny<TypeCheckException>(() =>
            new TypeChecker().Check(TestHarness.ParseOrThrow(source)));
        Assert.Equal(code, error.Diagnostic.TsCode);
    }

    [Theory]
    [InlineData("...[] as const", "TS2554")]
    [InlineData("...[1, 'two', 3] as const", "TS2554")]
    [InlineData("...['bad'] as const", "TS2345")]
    [InlineData("...[1, 'two']", "TS2556")]
    [InlineData("...([] as any as [number?])", "TS2345")]
    public void FixedParametersRetainTupleArityAndTypeChecks(string arguments, string code)
    {
        var source = $$"""
            class C {
                static #m(first: number, second?: string) {}
                static read() { this.#m({{arguments}}); }
            }
            """;
        var error = Assert.ThrowsAny<TypeCheckException>(() =>
            new TypeChecker().Check(TestHarness.ParseOrThrow(source)));
        Assert.Equal(code, error.Diagnostic.TsCode);
    }
}
