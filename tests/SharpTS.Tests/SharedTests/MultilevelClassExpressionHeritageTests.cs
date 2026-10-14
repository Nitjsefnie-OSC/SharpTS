using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class MultilevelClassExpressionHeritageTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["implicit", """
            const C = class { value = 43; read() { return this.value; } };
            class Middle extends C {}
            class Derived extends Middle {}
            const value: any = new Derived();
            console.log(value.value, value.read(), value instanceof C, value instanceof Middle, value instanceof Derived);
            """, "43 43 true true true\n"];
        yield return ["explicit", """
            const C = class { value: any; constructor(value: any) { this.value = value; } read() { return this.value; } };
            class Middle extends C { middle = 44; constructor(value: any) { super(value); } }
            class Derived extends Middle { derived = 45; constructor(value: any) { super(value); } }
            const value: any = new Derived(43);
            console.log(value.value, value.read(), value.middle, value.derived);
            console.log(value instanceof C, value instanceof Middle, value instanceof Derived);
            """, "43 43 44 45\ntrue true true\n"];
        yield return ["arguments", """
            const C = class {
                constructor(value: any = 44) { console.log(value, arguments.length, arguments[1], arguments[2]); }
            };
            class Middle extends C {}
            class Derived extends Middle {}
            const Construct: any = Derived;
            new Construct(43, "tail", true);
            new Construct();
            new Construct(undefined);
            const Bound: any = Construct.bind({}, null);
            new Bound("last", false);
            """, "43 3 tail true\n44 0 undefined undefined\n44 1 undefined undefined\nnull 3 last false\n"];
        yield return ["clr-chain", """
            class Base {
                value: any;
                constructor(value: any) { this.value = value; console.log("base", value); }
                read() { return this.value; }
            }
            class Static extends Base { ordinary = 44; }
            const Parent: any = Static;
            class Dynamic extends Parent { dynamic = 45; }
            class Derived extends Dynamic { derived = 46; }
            const Construct: any = Derived;
            const value: any = new Construct(43);
            console.log(value.value, value.read(), value.ordinary, value.dynamic, value.derived);
            console.log(value instanceof Base, value instanceof Static, value instanceof Dynamic, value instanceof Derived);
            """, "base 43\n43 43 44 45 46\ntrue true true true\n"];
        yield return ["evaluated-definition", """
            function make(tag: any) {
                const C = class Named { value = tag; read() { return Named; } };
                class Middle extends C {}
                class Derived extends Middle {}
                return { C, Middle, Derived };
            }
            const left: any = make(43);
            const right: any = make(44);
            const a: any = new left.Derived();
            const b: any = new right.Derived();
            console.log(a.value, b.value, a.read() === left.C, b.read() === right.C);
            console.log(a instanceof left.C, a instanceof left.Middle, a instanceof right.C, b instanceof right.C);
            """, "43 44 true true\ntrue true false true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void SingleSourceInitializesTheEntireGuestParentChain(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled) Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ModuleInitializesTheEntireGuestParentChain(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(new Dictionary<string, string> { ["main.ts"] = source }, "main.ts", mode));
    }

    [Theory]
    [InlineData(ExecutionMode.Interpreted)]
    [InlineData(ExecutionMode.Compiled)]
    public void ImportedExpressionParentRetainsItsDefinitionThroughDeclaredSubclasses(ExecutionMode mode)
    {
        var modules = new Dictionary<string, string>
        {
            ["base.ts"] = "export const C = class Named { value = 43; read() { return Named; } };",
            ["middle.ts"] = "import { C } from './base'; export class Middle extends C {}",
            ["main.ts"] = """
                import { C } from './base';
                import { Middle } from './middle';
                class Derived extends Middle {}
                const value: any = new Derived();
                console.log(value.value, value.read() === C, value instanceof C, value instanceof Middle, value instanceof Derived);
                """
        };
        Assert.Equal("43 true true true true\n", TestHarness.RunModules(modules, "main.ts", mode));
    }
}
