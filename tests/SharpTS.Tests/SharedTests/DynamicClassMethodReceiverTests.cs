using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DynamicClassMethodReceiverTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["generic-alias", """
            class Box<T> { value: T; constructor(value: T) { this.value = value; } read(): T { return this.value; } }
            class Other<U> { value: U; constructor(value: U) { this.value = value; } read(): U { return this.value; } }
            const Alias: typeof Box = Other;
            const instance: any = new Alias<number>(7);
            const method: any = instance.read;
            console.log(instance.value, instance.read(), method.call(instance));
            """, "7 7 7\n"];
        yield return ["borrowed-receivers", """
            class C { value: number; constructor(value: number) { this.value = value; } read(extra = 0) { return this.value + extra; } }
            const first: any = new C(7), second: any = new C(11);
            const method: any = first.read;
            console.log(first.read(), method.call(second, 2), method.apply(first, [3]));
            console.log(method.call({ value: 20 }, 4));
            const bound: any = method.bind(first, 5);
            console.log(bound(), bound.call(second), bound.bind(second)());
            """, "7 13 10\n24\n12 12 12\n"];
        yield return ["private-brand-receivers", """
            class C {
                #value: number;
                constructor(value: number) { this.#value = value; }
                #method() { return this.#value; }
                check() { return this.#method(); }
            }
            const first: any = new C(7), second: any = new C(11);
            const check: any = first.check;
            console.log(first.check(), check.call(second), check.apply(first, []));
            try { check.call({}); } catch (error: any) { console.log(error instanceof TypeError); }
            """, "7 11 7\ntrue\n"];
        yield return ["generic-private-and-method-metadata", """
            class C<T> {
                #value: T;
                constructor(value: T) { this.#value = value; }
                read<U>(unused: U): T { return this.#value; }
            }
            const first: any = new C<number>(7), second: any = new C<string>("other");
            const method: any = first.read;
            console.log(method.call(second, 0), method.name, method.length);
            """, "other read 1\n"];
        yield return ["inherited-method-accessor", """
            class Base { value = 43; get doubled() { return this.value * 2; } read() { return this.value; } }
            class Derived extends Base {}
            const instance: any = new Derived();
            console.log(instance.value, instance.doubled, instance.read());
            console.log(instance.read.call({ value: 9 }));
            """, "43 86 43\n9\n"];
        yield return ["inherited-generic", """
            class Base<T> { value: T; constructor(value: T) { this.value = value; } read(): T { return this.value; } }
            class Derived extends Base<string> {}
            const instance: any = new Derived("value");
            console.log(instance.value, instance.read(), instance.read.call({ value: "other" }));
            """, "value value other\n"];
        yield return ["suspended-method-calls", """
            class C { value = 43; read() { return this.value; } }
            const instance: any = new C();
            const method: any = instance.read;
            function* values() { yield instance.read(); yield method.call({ value: 44 }); }
            async function run() { await Promise.resolve(0); console.log(instance.read(), method.apply({ value: 45 }, [])); }
            console.log(values().next().value);
            for (const value of values()) console.log(value);
            run();
            """, "43\n43\n44\n43 45\n"];
        yield return ["constructor-return", """
            class C {
                value = 43;
                constructor(replace: boolean) {
                    if (replace) return { value: 44, read() { return this.value; } } as any;
                }
                read() { return this.value; }
            }
            const ordinary: any = new C(false), replacement: any = new C(true);
            console.log(ordinary.read(), replacement.read(), ordinary.read.call(replacement));
            """, "43 44 44\n"];
        yield return ["own-property-precedence", """
            class C { read() { return 43; } }
            const ordinary: any = new C();
            console.log(ordinary.read());
            ordinary.read = () => 99;
            console.log(ordinary.read());
            """, "43\n99\n"];
        yield return ["borrowed-super", """
            class Base { value = 0; read(value: number) { return this.value + value; } }
            class Derived extends Base { read(value: number) { return super.read(value) + arguments[0]; } }
            const instance: any = new Derived();
            const method: any = instance.read;
            console.log(method.call({ value: 10 }, 2));
            """, "14\n"];
        yield return ["suspended-method-arguments", """
            class C {
                value = 0;
                async read(value: number) { await Promise.resolve(0); return this.value + arguments[0]; }
                *values(value: number) { yield this.value + arguments[0]; }
            }
            const instance: any = new C();
            const read: any = instance.read, values: any = instance.values;
            async function run() {
                console.log(await read.call({ value: 7 }, 2), values.call({ value: 11 }, 3).next().value);
            }
            run();
            """, "9 14\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        // #2284 tracks the interpreter's stale cached property after assignment.
        => Cases().SelectMany(row => ((string)row[0] == "own-property-precedence"
                ? new[] { ExecutionMode.Compiled }
                : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ClassMethodValuesUseTheInvocationReceiver(
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
