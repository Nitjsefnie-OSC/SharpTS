using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionHeritageDispatchTests
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
        yield return ["alias", """
            const C = class { value = 43; };
            const Parent = C;
            class Derived extends Parent {}
            const value: any = new Derived();
            console.log(value.value);
            """, "43\n"];
        yield return ["inherited-members", """
            const C = class {
                value = 43;
                read() { return this.value; }
            };
            class Derived extends C {}
            const value: any = new Derived();
            console.log(value.value, value.read());
            """, "43 43\n"];
        yield return ["declared-base-control", """
            class Base {
                value = 43;
                get doubled() { return this.value * 2; }
                read() { return this.value; }
            }
            class Derived extends Base {}
            const value: any = new Derived();
            console.log(value.value, value.doubled, value.read());
            """, "43 86 43\n"];
        yield return ["expression-base-control", """
            const Base = class { value = 43; read() { return this.value; } };
            const Derived = class extends Base {};
            const value: any = new Derived();
            console.log(value.value, value.read());
            """, "43 43\n"];
        yield return ["generic-base-control", """
            class Base<T> {
                value: T;
                constructor(value: T) { this.value = value; }
                read(): T { return this.value; }
            }
            class Derived extends Base<string> {}
            const value: any = new Derived("value");
            console.log(value.value, value.read());
            """, "value value\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var testCase in Cases())
        {
            yield return [.. testCase, ExecutionMode.Interpreted];
            yield return [.. testCase, ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void PropertyDispatchUsesTheActualParentOwner(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        // The reported verifier failure occurs in module compilation, including
        // the CLI's single-file module path. The single-source Compile API has
        // separate preexisting constructor-forwarding limitations.
        Assert.Equal(expected, TestHarness.RunModules(
            new Dictionary<string, string> { ["main.ts"] = source }, "main.ts", mode));
    }
}
