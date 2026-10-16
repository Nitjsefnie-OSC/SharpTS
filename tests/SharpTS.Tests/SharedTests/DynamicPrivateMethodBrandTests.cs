using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DynamicPrivateMethodBrandTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["dynamic-instance", """
            class C { #method() { return 1; } check() { return this.#method(); } }
            const value: any = new C();
            console.log(value.check());
            """, "1\n"];
        yield return ["typed-control", """
            class C { #method() { return 1; } check() { return this.#method(); } }
            const value = new C();
            console.log(value.check());
            """, "1\n"];
        yield return ["brand-negative-control", """
            class C {
                #value: number;
                constructor(value: number) { this.#value = value; }
                #method() { return this.#value; }
                check() { return this.#method(); }
                static check(value: any) { return value.#method(); }
            }
            const first: any = new C(7), second: any = new C(11);
            console.log(first.check(), second.check(), C.check(first));
            try { C.check({}); }
            catch (error: any) { console.log(error instanceof TypeError); }
            """, "7 11 7\ntrue\n"];
        yield return ["inherited-public-method", """
            class Base { #method() { return 7; } check() { return this.#method(); } }
            class Derived extends Base {}
            const value: any = new Derived();
            console.log(value.check());
            """, "7\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void DynamicPublicCallsRetainTheirPrivateBrandReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
