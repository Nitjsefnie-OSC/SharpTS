using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticGetterAsyncArrowTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class C { static get arrow(): any { return async () => this; } }
            const owner: any = C, arrow: any = owner.arrow;
            console.log(typeof arrow);
            arrow().then((value: any) => console.log(value === C));
            """, "function\ntrue\n"];
        yield return ["receivers-and-descriptor", """
            class Base { static get arrow(): any { return async () => { await Promise.resolve(0); return this; }; } }
            class Derived extends Base {}
            const owner: any = Base, derived: any = Derived;
            const direct: any = Base.arrow, alias: any = owner.arrow, inherited: any = derived.arrow;
            const descriptor: any = Object.getOwnPropertyDescriptor(Base, "arrow"), receiver = { tag: "receiver" };
            const extracted: any = descriptor.get.call(receiver);
            console.log(typeof direct, typeof alias, typeof inherited, typeof extracted);
            Promise.all([direct(), alias(), inherited(), extracted()]).then((values: any[]) => {
                console.log(values[0] === Base, values[1] === Base, values[2] === Derived, values[3] === receiver);
            });
            """, "function function function function\ntrue true true true\n"];
        yield return ["expression-and-generic", """
            const Expression = class { static get arrow(): any { return async () => this; } };
            class Generic<T> { static get arrow(): any { return async () => this; } }
            const expression: any = Expression, generic: any = Generic;
            const first: any = expression.arrow, second: any = generic.arrow;
            console.log(typeof first, typeof second);
            Promise.all([first(), second()]).then((values: any[]) => {
                console.log(values[0] === Expression, values[1] === Generic);
            });
            """, "function function\ntrue true\n"];
        yield return ["instance-and-sync-controls", """
            class C {
                slot = 7;
                get arrow(): any { return async () => { await Promise.resolve(0); return this; }; }
                static get sync(): any { return () => this; }
            }
            const instance: any = new C(), arrow: any = instance.arrow, owner: any = C;
            console.log(typeof arrow, typeof owner.sync, owner.sync() === C);
            arrow().then((value: any) => console.log(value === instance, value.slot));
            """, "function function true\ntrue 7\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AccessorAsyncArrowsRemainCallableAndCaptureTheirGuestReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
