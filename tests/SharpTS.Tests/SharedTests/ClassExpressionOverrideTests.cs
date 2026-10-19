using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionOverrideTests
{
    [Theory, ModeData]
    public void InstanceAndStaticAccessorsSupportOverride(ExecutionMode mode)
    {
        const string source = """
            class Base { get value() { return 1; } }
            const Derived = class extends Base { override get value() { return 2; } };
            console.log(new Derived().value);
            class AccessorBase {
                stored = 0;
                get count(): number { return this.stored; }
                set count(next: number) { this.stored = next; }
                static get value(): number { return 1; }
            }
            const AccessorChild = class extends AccessorBase {
                override get count(): number { return this.stored * 2; }
                override set count(next: number) { this.stored = next + 1; }
                static override get value(): number { return 3; }
            };
            const child = new AccessorChild();
            child.count = 4;
            console.log(child.count, AccessorChild.value, AccessorBase.value);
            """;
        Assert.Equal("2\n10 3 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void OrdinaryComputedAsyncAndGeneratorMethodsSupportOverride(ExecutionMode mode)
    {
        const string source = """
            const key = "computed";
            class Base {
                value() { return 1; }
                [key]() { return 1; }
                static value() { return 1; }
                *values(): any { yield 1; }
                async read(): Promise<number> { return 1; }
            }
            const Derived = class extends Base {
                override value() { return 2; }
                override [key]() { return 3; }
                static override value() { return 4; }
                override *values(): any { yield 5; }
                override async read(): Promise<number> { return 6; }
            };
            const child = new Derived();
            console.log(child.value(), child[key](), Derived.value(), child.values().next().value);
            async function main() { console.log(await child.read()); }
            main();
            """;
        Assert.Equal("2 3 4 5\n6\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void OverrideMemberNamesKeepTheirOrdinaryBehavior(ExecutionMode mode)
    {
        const string source = """
            const Method = class { override() { return 7; } };
            const Generic = class { override<T>(value: T): T { return value; } };
            const Field = class { override = 8; };
            const Static = class { static override() { return 9; } };
            console.log(new Method().override(), new Generic().override<number>(10),
                new Field().override, Static.override());
            """;
        Assert.Equal("7 10 8 9\n", TestHarness.Run(source, mode));
    }

    public static IEnumerable<object[]> InvalidCases()
    {
        string[] sources =
        [
            "class Base {} const Derived = class extends Base { override value() { return 1; } };",
            "class Base {} const Derived = class extends Base { override get value() { return 1; } };",
            "class Base {} const Derived = class extends Base { override set value(next: number) {} };",
            "class Base { value() { return 1; } } const Derived = class extends Base { static override value() { return 2; } };",
            "class Base { static value(): number { return 1; } } const Derived = class extends Base { static override value(): string { return 'bad'; } };",
        ];
        foreach (string source in sources)
            foreach (ExecutionMode mode in new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
                yield return [source, mode];
    }

    [Theory, MemberData(nameof(InvalidCases))]
    public void InvalidOverridesAreRejectedBeforeExecution(string source, ExecutionMode mode) =>
        Assert.IsAssignableFrom<TypeCheckException>(Assert.ThrowsAny<Exception>(() => TestHarness.Run(source, mode)));
}
