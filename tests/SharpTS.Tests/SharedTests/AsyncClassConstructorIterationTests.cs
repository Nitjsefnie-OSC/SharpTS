using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncClassConstructorIterationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class C { static *[Symbol.iterator]() { yield 42; } }
            const iterable: any = C;
            async function run() {
                try {
                    for (const value of iterable) console.log(value);
                    console.log("done");
                } catch (error) { console.log("caught", (error as any).message); }
            }
            run();
            """, "42\ndone\n"];
        yield return ["receivers", """
            class Base { static *[Symbol.iterator]() { yield this; } }
            class Derived extends Base {}
            const expression: any = class { static *[Symbol.iterator]() { yield this; } };
            const inherited: any = Derived;
            async function run() {
                for (const constructor of [inherited, expression]) {
                    for (const value of constructor) console.log(value === constructor);
                }
            }
            run();
            """, "true\ntrue\n"];
        yield return ["getters", """
            let gets = 0, calls = 0, evaluations = 0;
            class C {
                static *[Symbol.iterator]() { calls++; console.log(this === C); yield 1; yield 2; }
            }
            const iterable: any = C, method: any = iterable[Symbol.iterator];
            Object.defineProperty(iterable, Symbol.iterator, {
                get: () => { gets++; return method; }, configurable: true
            });
            function source(): any { evaluations++; return iterable; }
            async function run() {
                for (const value of source()) console.log(value);
                console.log(gets, calls, evaluations);
            }
            run();
            """, "true\n1\n2\n1 1 1\n"];
        yield return ["pending", """
            class C { static *[Symbol.iterator]() { yield 1; yield 2; yield 3; } }
            const iterable: any = C;
            async function run() {
                let values = "";
                for (const value of iterable) {
                    await new Promise<number>(resolve => setTimeout(() => resolve(0), 1));
                    values += value;
                }
                console.log("function", values);
                const arrow = async () => {
                    let values = "";
                    for (const value of iterable) {
                        await new Promise<number>(resolve => setTimeout(() => resolve(0), 1));
                        values += value;
                    }
                    console.log("arrow", values);
                };
                await arrow();
                let labeled = "";
                outer: inner: for (const value of iterable) {
                    await new Promise<number>(resolve => setTimeout(() => resolve(0), 1));
                    if (value === 1) continue outer;
                    labeled += value;
                    if (value === 2) break inner;
                }
                console.log("labels", labeled);
            }
            run();
            """, "function 123\narrow 123\nlabels 2\n"];
        yield return ["generator", """
            class C { static *[Symbol.iterator]() { yield 5; yield 6; } }
            const iterable: any = C;
            async function* values() {
                for (const value of iterable) {
                    await new Promise<number>(resolve => setTimeout(() => resolve(0), 1));
                    yield value;
                }
            }
            async function run() {
                for await (const value of values()) console.log(value);
                console.log("done");
            }
            run();
            """, "5\n6\ndone\n"];
        yield return ["fallback", """
            async function run() {
                for (const value of [1, 2]) console.log(value);
                for (const value of "ab") console.log(value);
            }
            run();
            """, "1\n2\na\nb\n"];
        yield return ["awaited-iterable", """
            class C { static *[Symbol.iterator]() { yield 42; } }
            const iterable: any = C;
            async function run() {
                for (const value of await Promise.resolve(iterable)) console.log(value);
                console.log("done");
            }
            run();
            """, "42\ndone\n"];
        yield return ["invalid-method", """
            class C {}
            const iterable: any = C;
            iterable[Symbol.iterator] = 7;
            async function run() {
                try { for (const value of iterable) {} console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            run();
            """, "true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void SynchronousGuestIterationPreservesAcquisitionAndSuspension(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled) Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void CompiledPresentNullIteratorMethodDoesNotFallBackToClrEnumeration()
    {
        const string source = """
            class C {}
            const iterable: any = C;
            iterable[Symbol.iterator] = null;
            async function run() {
                try { for (const value of iterable) {} console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            run();
            """;
        Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal("true\n", TestHarness.Run(source, ExecutionMode.Compiled));
    }
}
