using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GroupedSuperReceiverTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class A { label = 'a'; value() { return this.label; } }
            class B extends A {
                label = 'b';
                read() { return (() => (super.value)())(); }
                asserted() { return (() => (super.value as any)())(); }
            }
            class C extends B { label = 'c'; }
            console.log(new C().read(), new C().asserted());
            """, "c c\n"];
        yield return ["nested-reference-wrappers", """
            class A { label = 'base'; value() { return this.label; } }
            class B extends A {
                label = 'child';
                read() { return (((super.value as any)!))(); }
                checked() { return ((super.value satisfies (() => string)))(); }
            }
            const instance: any = new B();
            console.log(instance.read(), instance.checked());
            """, "child child\n"];
        yield return ["borrowed-method-and-captured-arrow", """
            class A { label = 'base'; value() { return this.label; } }
            class B extends A { make() { return () => ((super.value as any))(); } }
            const make: any = (B.prototype as any).make;
            const receiver: any = { label: 'borrowed' };
            const read = make.call(receiver);
            receiver.label = 'updated';
            console.log(read.call({ label: 'ignored' }));
            """, "updated\n"];
        yield return ["detached-value-control", """
            class A { self(): any { return this; } }
            class B extends A {
                read() {
                    const detached: any = super.self;
                    console.log((super.self)() === this, (super.self as any)() === this);
                    console.log(detached() === undefined);
                }
            }
            new B().read();
            """, "true true\ntrue\n"];
        yield return ["getter-reference-receiver", """
            const state: any = { receiver: null };
            class A {
                get value(): any {
                    state.receiver = this;
                    return function(this: any) { return this.label; };
                }
            }
            class B extends A { label = 'child'; read() { return (super.value as any)(); } }
            const instance: any = new B();
            console.log(instance.read(), state.receiver === instance);
            """, "child true\n"];
        yield return ["async-and-generator-contexts", """
            class A { label = 'base'; value() { return this.label; } }
            class B extends A {
                label = 'child';
                async read() { await Promise.resolve(0); return ((super.value as any))(); }
                *values() { yield (super.value)(); yield (super.value as any)(); }
            }
            async function run() {
                const instance: any = new B();
                console.log(await instance.read());
                for (const value of instance.values()) console.log(value);
            }
            run();
            """, "child\nchild\nchild\n"];
        yield return ["class-expression", """
            class A { value(): any { return this; } }
            const B: any = class extends A { read() { return (() => (super.value as any)())(); } };
            const instance: any = new B();
            console.log(instance.read() === instance);
            """, "true\n"];
        yield return ["optional-wrapped-reference", """
            const state: any = { calls: 0 };
            function next() { state.calls++; return state.calls; }
            class A { value(x: number) { return this.label + ':' + x; } label = 'base'; }
            class B extends A { label = 'child'; read() { return (super.value as any)?.(next()); } }
            const instance: any = new B();
            console.log(instance.read(), state.calls);
            (A.prototype as any).value = null;
            console.log(instance.read() === undefined, state.calls);
            """, "child:1 1\ntrue 1\n"];
        yield return ["static-inherited-receiver", """
            class A { static value(): any { return this; } }
            class B extends A { static read() { return (() => ((super.value as any)! )())(); } }
            class C extends B {}
            console.log(B.read() === B, C.read() === C);
            """, "true true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ReferencePreservingWrappersRetainTheLexicalReceiver(
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
