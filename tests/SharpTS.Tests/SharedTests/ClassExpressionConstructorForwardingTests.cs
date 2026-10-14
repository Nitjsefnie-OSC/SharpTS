using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionConstructorForwardingTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["implicit", """
            const C = class { value: number; constructor(value: number) { this.value = value; } };
            class Derived extends C {}
            const value: any = new Derived(43);
            console.log(value.value);
            """, "43\n"];
        yield return ["explicit", """
            const C = class { value: number; constructor(value: number) { this.value = value; } };
            class Derived extends C { constructor(value: number) { super(value); } }
            const value: any = new Derived(43);
            console.log(value.value);
            """, "43\n"];
        yield return ["definition", """
            const C = class Named { value = 43; read() { return Named; } };
            class Derived extends C {}
            const value: any = new Derived();
            console.log(value.value, value.read() === C, value instanceof C, value instanceof Derived);
            """, "43 true true true\n"];
        yield return ["defaults-and-surplus", """
            const C = class {
                constructor(value: any = 44, flag: any = false) {
                    console.log(value, flag, arguments.length, arguments[2]);
                }
            };
            class Derived extends C {}
            const Construct: any = Derived;
            new Construct(43, true, "tail");
            new Construct();
            new Construct(undefined, undefined);
            const Bound: any = Construct.bind({}, null);
            new Bound(null, "last");
            """, "43 true 3 tail\n44 false 0 undefined\n44 false 2 undefined\nnull null 3 last\n"];
        yield return ["declaration-rest", """
            const C = class { constructor(value: any) { console.log("base", value); } };
            class Derived extends C {
                constructor(value: any = 44, ...rest: any[]) {
                    super(value);
                    console.log(rest.length, rest.length ? rest[0] : "empty", arguments.length, arguments[2]);
                }
            }
            const Construct: any = Derived;
            new Construct(43, "tail", true);
            new Construct();
            """, "base 43\n2 tail 3 true\nbase 44\n0 empty 0 undefined\n"];
        yield return ["typed-slot", """
            class Box {}
            class Base { constructor(value: Box) { console.log(value instanceof Box); } }
            const D = class extends Base {};
            new D(new Box());
            """, "true\n"];
        yield return ["generic-declaration-slot", """
            class Base<T> { constructor(value: T) { console.log(value); } }
            const D = class extends Base<number> {};
            new D(43);
            """, "43\n"];
        yield return ["generic-declaration-child", """
            class Base<T> { constructor(value: T) { console.log(value, typeof value); } }
            const D = class<T> extends Base<T> {};
            new D<number>(43);
            """, "43 number\n"];
    }

    public static IEnumerable<object[]> SingleSourceControls()
    {
        yield return ["private-accessor", """
            const C = class {
                #value = 43;
                get value() { return this.#value; }
                set value(value: number) { this.#value = value; }
                read() { return this.#value; }
            };
            class Derived extends C {}
            const value: any = new Derived();
            console.log(value.value, value.read());
            value.value = 44;
            console.log(value.value, value.read(), value instanceof C);
            """, "43 43\n44 44 true\n"];
        yield return ["generic-expression", """
            const C = class<T> { value: T; constructor(value: T) { this.value = value; } read() { return this.value; } };
            class Derived extends C<string> {}
            const value: any = new Derived("value");
            console.log(value.value, value.read(), value instanceof C);
            """, "value value true\n"];
        yield return ["evaluated-parent", """
            function make(tag: any) {
                const C = class Named { value = tag; read() { return Named; } };
                class Derived extends C {}
                return { C, Derived };
            }
            const left: any = make(43);
            const right: any = make(44);
            const a: any = new left.Derived();
            const b: any = new right.Derived();
            console.log(a.value, b.value, a.read() === left.C, b.read() === right.C);
            console.log(a instanceof left.C, a instanceof right.C, b instanceof right.C);
            """, "43 44 true true\ntrue false true\n"];
        yield return ["replacement", """
            const replacement: any = { value: 43 };
            const C = class { constructor() { return replacement; } };
            class Derived extends C {}
            const value: any = new Derived();
            console.log(value === replacement, value.value);
            """, "true 43\n"];
    }

    public static IEnumerable<object[]> SingleModeCases() => WithModes(Cases()
        .Where(row => row[0] is not ("generic-declaration-slot" or "generic-declaration-child"))
        .Concat(SingleSourceControls()));
    public static IEnumerable<object[]> ModuleModeCases() => WithModes(Cases());
    private static IEnumerable<object[]> WithModes(IEnumerable<object[]> cases) => cases.SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(SingleModeCases))]
    public void SingleSourcePreservesExpressionParentConstruction(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled) Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ModuleModeCases))]
    public void ModuleConstructionPreservesExpressionParentArguments(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(new Dictionary<string, string> { ["main.ts"] = source }, "main.ts", mode));
    }
}
