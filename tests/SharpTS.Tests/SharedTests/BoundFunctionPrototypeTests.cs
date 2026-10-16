using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BoundFunctionPrototypeTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-inherited-handler", """
            class Base {
                static [Symbol.hasInstance](value: any) { return this === Derived && value === null; }
            }
            class Derived extends Base {}
            const target: any = Derived;
            const Bound: any = target.bind({});
            const Twice: any = Bound.bind({});
            console.log(null instanceof target, null instanceof Twice, {} instanceof target);
            console.log(Object.getPrototypeOf(Bound) === Base, Object.getPrototypeOf(Twice) === Base);
            console.log(Function.prototype[Symbol.hasInstance].call(Bound, null));
            """, "true false false\ntrue true\ntrue\n"];
        yield return ["snapshot-and-explicit-mutation", """
            function Target() { return 42; }
            const first: any = { marker: "first", [Symbol.hasInstance](value: any) { return value === 1; } };
            const second: any = { marker: "second", [Symbol.hasInstance](value: any) { return value === 2; } };
            const bind: any = Function.prototype.bind;
            Object.setPrototypeOf(Target, first);
            const Bound: any = Reflect.apply(bind, Target, [null]);
            Object.setPrototypeOf(Target, second);
            const Twice: any = Reflect.apply(bind, Bound, [null]);
            const Fresh: any = Reflect.apply(bind, Target, [null]);
            console.log(Object.getPrototypeOf(Bound) === first, Object.getPrototypeOf(Twice) === first);
            console.log(Object.getPrototypeOf(Fresh) === second);
            console.log(Bound.marker, Twice.marker, Reflect.get(Bound, "marker"));
            first.marker = "changed";
            console.log(Bound.marker, Twice.marker);
            console.log(1 instanceof Bound, 1 instanceof Twice, 2 instanceof Target);
            Object.setPrototypeOf(Bound, second);
            console.log(Object.getPrototypeOf(Bound) === second, Object.getPrototypeOf(Twice) === first);
            console.log(Bound.marker, Twice.marker, 2 instanceof Bound, 1 instanceof Twice);
            Object.setPrototypeOf(Bound, null);
            console.log(Object.getPrototypeOf(Bound) === null, typeof Bound.bind, Bound());
            console.log(2 instanceof Bound);
            """, "true true\ntrue\nfirst first first\nchanged changed\ntrue true true\ntrue true\nsecond changed true true\ntrue undefined 42\ntrue\n"];
        yield return ["null-snapshot", """
            function Target(value: number) { return value + 1; }
            const bind: any = Function.prototype.bind;
            Object.setPrototypeOf(Target, null);
            const Bound: any = Reflect.apply(bind, Target, [null, 4]);
            const Twice: any = Reflect.apply(bind, Bound, [null]);
            console.log(Object.getPrototypeOf(Bound) === null, Object.getPrototypeOf(Twice) === null);
            console.log(typeof Bound.bind, Bound(), Twice(), Bound.name, Bound.length);
            """, "true true\nundefined 5 5 bound Target 0\n"];
        yield return ["getter-receiver", """
            function Target() {}
            let Bound: any;
            let Twice: any;
            const custom: any = {};
            Object.defineProperty(custom, Symbol.hasInstance, {
                get() {
                    console.log(this === Bound, this === Twice);
                    return function(value: any) {
                        console.log(this === Bound, this === Twice);
                        return value === 7;
                    };
                }
            });
            Object.setPrototypeOf(Target, custom);
            Bound = Reflect.apply(Function.prototype.bind, Target, [null]);
            Twice = Reflect.apply(Function.prototype.bind, Bound, [null]);
            console.log(7 instanceof Bound, 8 instanceof Twice);
            """, "true false\ntrue false\nfalse true\nfalse true\ntrue false\n"];
        yield return ["invocation-and-construction", """
            function sum(this: any, a: number, b: number) { return this.start + a + b; }
            const bound: any = sum.bind({ start: 1 }, 2);
            const twice: any = bound.bind({ start: 90 }, 3);
            console.log(twice());
            class C { total: number; constructor(a: number, b: number) { this.total = a + b; } }
            const Bound: any = C.bind({}, 4);
            const Twice: any = Bound.bind({}, 5);
            const value: any = new Twice();
            console.log(value.total, value instanceof C, value instanceof Bound, value instanceof Twice);
            """, "6\n9 true true true\n"];
        yield return ["own-handler-precedence-and-fallback", """
            class C {}
            const target: any = C;
            const Bound: any = target.bind(null);
            Object.defineProperty(Bound, Symbol.hasInstance, {
                configurable: true,
                value: function(value: any) { console.log(this === Bound); return value === 7; }
            });
            console.log(7 instanceof Bound, 8 instanceof Bound);
            Object.defineProperty(Bound, Symbol.hasInstance, { value: null });
            console.log(new C() instanceof Bound);
            Object.defineProperty(Bound, Symbol.hasInstance, { value: undefined });
            console.log(new C() instanceof Bound);
            Object.defineProperty(Bound, Symbol.hasInstance, { value: 1 });
            try { console.log(new C() instanceof Bound); } catch (error) { console.log(error instanceof TypeError); }
            console.log(delete Bound[Symbol.hasInstance], new C() instanceof Bound);
            console.log(Object.hasOwn(Bound, "prototype"));
            """, "true\ntrue\ntrue false\ntrue\ntrue\ntrue\ntrue true\nfalse\n"];
        yield return ["writable-metadata", """
            function F(value: number) {}
            const Bound: any = F.bind(null);
            Object.defineProperty(Bound, "length", { value: 10, writable: true, configurable: true });
            Object.defineProperty(Bound, "name", { value: "renamed", writable: true, configurable: true });
            Bound.length = 12;
            Bound.name = "assigned";
            console.log(Bound.length, Bound.name);
            console.log(Object.getOwnPropertyDescriptor(Bound, "length").value);
            delete Bound.length;
            Object.setPrototypeOf(Bound, { length: 8 });
            console.log(Bound.length, Object.getOwnPropertyDescriptor(Bound, "length"));
            Object.defineProperty(Bound, "name", { value: "locked", configurable: false });
            console.log(Reflect.deleteProperty(Bound, "name"));
            const remove = function() { "use strict"; return delete Bound.name; };
            try { remove(); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Bound.name);
            """, "12 assigned\n12\n8 undefined\nfalse\ntrue\nlocked\n"];
        yield return ["cycles-and-nonextensibility", """
            function F() {}
            const Bound: any = F.bind(null);
            Object.setPrototypeOf(Bound, F);
            console.log(Reflect.setPrototypeOf(F, Bound));
            Object.preventExtensions(Bound);
            console.log(Reflect.setPrototypeOf(Bound, F), Reflect.setPrototypeOf(Bound, null));
            console.log(Object.getPrototypeOf(Bound) === F);
            """, "false\ntrue false\ntrue\n"];
        yield return ["sealed-deletion", """
            function F(value: number) {}
            const Bound: any = F.bind(null);
            delete Bound.length;
            Object.setPrototypeOf(Bound, { inherited: 9, length: 8 });
            Object.seal(Bound);
            console.log(delete Bound.absent, delete Bound.inherited, delete Bound.length);
            console.log(delete Bound.name, Reflect.deleteProperty(Bound, "name"));
            console.log(Bound.inherited, Bound.length, Bound.name, Object.isSealed(Bound));
            """, "true true true\nfalse false\n9 8 bound F true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void BoundFunctionsRetainPrototypeSnapshotsAndHandlerReceivers(
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

    // Compiled Reflect.deleteProperty with Symbol keys is tracked in #2160.
    [Theory, InterpretedOnlyData]
    public void ReflectDeleteRemovesOwnBoundSymbolHandler(ExecutionMode mode)
    {
        const string source = """
            class C {}
            const target: any = C;
            const Bound: any = target.bind(null);
            Object.defineProperty(Bound, Symbol.hasInstance, { configurable: true, value: () => false });
            console.log(new C() instanceof Bound);
            console.log(Reflect.deleteProperty(Bound, Symbol.hasInstance), new C() instanceof Bound);
            """;
        Assert.Equal("false\ntrue true\n", TestHarness.Run(source, mode));
    }
}
