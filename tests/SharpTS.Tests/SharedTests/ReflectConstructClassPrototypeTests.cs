using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ReflectConstructClassPrototypeTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["alternate-function", """
            class C { value = 7; original() { return 0; } }
            function Alt() {}
            const alternate: any = Alt;
            alternate.prototype.marker = 42;
            const instance: any = Reflect.construct(C, [], alternate);
            console.log(Object.getPrototypeOf(instance) === alternate.prototype);
            console.log(instance.value, instance.marker, instance instanceof C, instance instanceof alternate);
            console.log(instance.constructor === alternate, typeof instance.original);
            """, "true\n7 42 false true\ntrue undefined\n"];
        yield return ["alternate-class", """
            class C { value = 7; }
            class Alternate { read() { return 42; } }
            const instance: any = Reflect.construct(C, [], Alternate);
            console.log(Object.getPrototypeOf(instance) === Alternate.prototype);
            console.log(instance.value, instance.read(), instance instanceof C, instance instanceof Alternate);
            """, "true\n7 42 false true\n"];
        yield return ["normal-class", """
            class C { value = 7; }
            const instance: any = Reflect.construct(C, []);
            console.log(Object.getPrototypeOf(instance) === C.prototype, instance.value, instance instanceof C);
            """, "true 7 true\n"];
        yield return ["constructor-observation", """
            let observed: any;
            class C { constructor() { observed = Object.getPrototypeOf(this); } }
            function Alt() {}
            const instance: any = Reflect.construct(C, [], Alt);
            console.log(observed === Alt.prototype, Object.getPrototypeOf(instance) === Alt.prototype);
            """, "true true\n"];
        yield return ["constructor-method-dispatch", """
            class C {
                constructor() { console.log(this.read(), this.tag); }
                read() { return "C"; }
                get tag() { return "C-getter"; }
            }
            class Alternate {
                read() { return "alternate"; }
                get tag() { return "alternate-getter"; }
            }
            Reflect.construct(C, [], Alternate);
            """, "alternate alternate-getter\n"];
        yield return ["typed-method-dispatch", """
            class C { read() { return 1; } }
            class Alternate { read() { return 7; } }
            const instance: C = Reflect.construct(C, [], Alternate);
            console.log(instance.read());
            """, "7\n"];
        yield return ["state-machine-method-dispatch", """
            class C { read(value: number) { return value + 1; } }
            class Alternate { read(value: number) { return value + 7; } }
            const instance: C = Reflect.construct(C, [], Alternate);
            function* values() { yield instance.read(2); }
            async function run() { console.log(instance.read(await Promise.resolve(3))); }
            console.log(values().next().value);
            run();
            """, "9\n10\n"];
        yield return ["normal-suspended-call", """
            class C { value = 11; read(value: number) { return this.value + value; } }
            async function run() {
                const instance: C = new C();
                Reflect.ownKeys(instance);
                const dynamic: any = instance;
                console.log(instance.read(await Promise.resolve(1)), dynamic.read(1));
            }
            run().then(() => console.log("done"));
            """, "12 12\ndone\n"];
        yield return ["derived-constructor", """
            let observed: any;
            class Base { base = 3; constructor() { observed = Object.getPrototypeOf(this); } }
            class C extends Base { value = 7; }
            function Alt() {}
            const instance: any = Reflect.construct(C, [], Alt);
            console.log(observed === Alt.prototype, instance.base, instance.value, instance instanceof C);
            """, "true 3 7 false\n"];
        yield return ["primitive-prototype", """
            class C { value = 7; }
            function Alt() {}
            const alternate: any = Alt;
            alternate.prototype = 1;
            const instance: any = Reflect.construct(C, [], alternate);
            console.log(Object.getPrototypeOf(instance) === Object.prototype, instance.value, instance instanceof C);
            """, "true 7 false\n"];
        yield return ["alternate-accessors", """
            let writes = 0;
            let receiver: any;
            class C { own = 7; constructor() { (this as any).value = 9; } }
            function Alt() {}
            Object.defineProperty(Alt.prototype, "value", {
                get() { return 13; },
                set(value: number) { writes = value; receiver = this; }
            });
            const instance: any = Reflect.construct(C, [], Alt);
            console.log(instance.own, instance.value, writes, receiver === instance);
            """, "7 13 9 true\n"];
        yield return ["explicit-return", """
            const replacement: any = { value: 9 };
            class C { constructor() { return replacement; } }
            function Alt() {}
            const instance: any = Reflect.construct(C, [], Alt);
            console.log(instance === replacement, Object.getPrototypeOf(instance) === Object.prototype);
            """, "true true\n"];
        yield return ["derived-prototype-order", """
            const log: string[] = [];
            function Alt() {}
            const alternate: any = new Proxy(Alt, {
                get(target: any, key: any, receiver: any) {
                    if (key === "prototype") log.push("prototype");
                    return Reflect.get(target, key, receiver);
                }
            });
            class Base {}
            class C extends Base {
                constructor() { log.push("before"); super(); log.push("after"); }
            }
            Reflect.construct(C, [], alternate);
            console.log(log.join(","));
            """, "before,prototype,after\n"];
        yield return ["derived-return-without-allocation", """
            const log: string[] = [];
            const replacement: any = {};
            function Alt() {}
            const alternate: any = new Proxy(Alt, {
                get(target: any, key: any, receiver: any) {
                    if (key === "prototype") log.push("prototype");
                    return Reflect.get(target, key, receiver);
                }
            });
            class Base {}
            class C extends Base {
                constructor() { log.push("body"); if (replacement) return replacement; super(); }
            }
            console.log(Reflect.construct(C, [], alternate) === replacement, log.join(","));
            """, "true body\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        // #2352 tracks compiled ThisUninitReturn for returns before super.
        // Retain the interpreter's no-allocation ordering check here.
        => Cases().SelectMany(row => ((string)row[0] == "derived-return-without-allocation"
                ? new[] { ExecutionMode.Interpreted }
                : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ClassAllocationUsesNewTargetPrototype(
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
