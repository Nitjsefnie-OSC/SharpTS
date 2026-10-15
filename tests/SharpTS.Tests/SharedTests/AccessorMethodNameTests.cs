using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AccessorMethodNameTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class C { accessor() { return 42; } }
            console.log(new C().accessor());
            """, "42\n"];
        yield return ["class-expressions", """
            const Anonymous = class { accessor() { return 43; } };
            const Named = class Local { accessor() { return 44; } static accessor() { return 45; } };
            console.log(new Anonymous().accessor(), new Named().accessor(), Named.accessor());
            """, "43 44 45\n"];
        yield return ["generic-static-and-access-modifiers", """
            class C {
                public accessor<T>(value: T): T { return value; }
                static accessor<T>(value: T): T { return value; }
            }
            class PrivateName {
                private accessor() { return 44; }
                read() { return this.accessor(); }
            }
            class Base { protected accessor() { return 45; } }
            class Derived extends Base { read() { return this.accessor(); } }
            class OverrideBase { accessor() { return 1; } }
            class Override extends OverrideBase { override accessor() { return 46; } }
            const Expression = class {
                accessor<T>(value: T): T { return value; }
                static accessor() { return 47; }
            };
            console.log(new C().accessor<number>(42), C.accessor<string>("static"),
                new PrivateName().read(), new Derived().read(), new Override().accessor());
            console.log(new Expression().accessor<string>("expression"), Expression.accessor());
            """, "42 static 44 45 46\nexpression 47\n"];
        yield return ["async-method-consumption", """
            class C {
                async accessor(value: number) { return value + 1; }
                static async accessor(value: number) { return value + 2; }
            }
            const Expression = class { async accessor(value: number) { return value + 3; } };
            async function run() {
                console.log(await new C().accessor(41), await C.accessor(41),
                    await new Expression().accessor(41));
            }
            run();
            """, "42 43 44\n"];
        yield return ["generator-and-async-generator-consumption", """
            class C {
                *accessor() { yield 41; return 42; }
                static *accessor() { yield 43; }
            }
            const Expression = class { *accessor() { yield 44; } };
            const iterator = new C().accessor();
            console.log(iterator.next().value, iterator.next().value, iterator.next().done,
                C.accessor().next().value, new Expression().accessor().next().value);
            class AsyncC {
                async *accessor() { yield 45; return 46; }
                static async *accessor() { yield 47; }
            }
            const AsyncExpression = class { async *accessor() { yield 48; } };
            async function run() {
                const iterator = new AsyncC().accessor();
                const first = await iterator.next(), second = await iterator.next(), done = await iterator.next();
                const fromStatic = await AsyncC.accessor().next();
                const fromExpression = await new AsyncExpression().accessor().next();
                console.log(first.value, second.value, done.done, fromStatic.value, fromExpression.value);
            }
            run();
            """, "41 42 true 43 44\n45 46 true 47 48\n"];
        yield return ["mixed-auto-manual-and-named-methods", """
            class C {
                accessor value: string = "one";
                accessor() { return this.value; }
                get manual(): string { return this.value; }
                set manual(value: string) { this.value = value; }
                static accessor label: string = "static";
                static accessor() { return this.label; }
            }
            const Expression = class {
                value: string = "expression";
                accessor() { return this.manual; }
                get manual(): string { return this.value; }
                set manual(value: string) { this.value = value; }
            };
            const c = new C(), expression = new Expression();
            console.log(c.accessor(), c.manual, C.accessor(), expression.accessor());
            c.value = "two"; c.manual = "three"; C.label = "changed"; expression.manual = "updated";
            console.log(c.accessor(), c.manual, C.accessor(), expression.accessor());
            class ManualName {
                value = "manual";
                get accessor(): string { return this.value; }
                set accessor(value: string) { this.value = value; }
            }
            const manual = new ManualName(); manual.accessor = "setter";
            console.log(manual.accessor);
            """, "one one static expression\nthree three changed updated\nsetter\n"];
        yield return ["literal-computed-and-field-names", """
            class Quoted { "accessor"() { return 41; } }
            const Computed = class { ["accessor"]() { return 42; } };
            const state = { keys: 0 };
            function key(): string { state.keys++; return "accessor"; }
            class Evaluated { [key()]() { return 43; } }
            const symbol = Symbol("accessor");
            class SymbolName { [symbol]() { return 44; } }
            console.log(new Quoted().accessor(), (new Computed() as any).accessor(),
                (new Evaluated() as any).accessor(), (new SymbolName() as any)[symbol](), state.keys);
            class Fields { accessor: string = "field"; static accessor: number = 45; }
            console.log(new Fields().accessor, Fields.accessor);
            """, "41 42 43 44 1\nfield 45\n"];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AccessorCanNameMethodsWithoutChangingAutoAccessors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
