using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DerivedParameterPropertyTests
{
    public const string IssueSource = """
        class Base { base = 1; }
        class Derived extends Base { constructor(public value: number) { super(); } }
        const derived = new Derived(44);
        console.log(derived.base, derived.value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["declaration", IssueSource, "1 44\n"];
        yield return ["expression", """
            class Base { base = 1; }
            const Derived = class extends Base { constructor(public value: number) { super(); } };
            const derived = new Derived(45);
            console.log(derived.base, derived.value);
            """, "1 45\n"];
        yield return ["quoted-constructor", IssueSource.Replace("constructor(", "\"constructor\"("), "1 44\n"];
        yield return ["ordering-and-parameter-updates", """
            const events: string[] = [];
            class Base { constructor() { events.push("super"); } }
            class Derived extends Base {
                own = (events.push("field"), 2);
                constructor(public value: number, readonly label: string = "ok") {
                    events.push("before"); value++;
                    super();
                    events.push("after:" + this.value);
                    this.value += 2;
                }
            }
            const derived = new Derived(44);
            console.log(derived.value, derived.label, derived.own, events.join(","));
            """, "47 ok 2 before,super,field,after:45\n"];
        yield return ["conditional-super", """
            class Base { base = 1; }
            class Derived extends Base {
                constructor(public value: number, flag: boolean) {
                    if (flag) { super(); console.log(this.value); }
                    else { super(); console.log(this.value); }
                }
            }
            const first = new Derived(2, true), second = new Derived(3, false);
            console.log(first.base, first.value, second.base, second.value);
            """, "2\n3\n1 2 1 3\n"];
        yield return ["super-expression-and-base-control", """
            class Base { constructor(public base: number) {} }
            class Derived extends Base {
                constructor(public value: number) { const receiver = super(1); console.log(receiver === this); }
            }
            const derived = new Derived(4), base = new Base(5);
            console.log(derived.base, derived.value, base.base);
            """, "true\n1 4 5\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ParameterPropertiesInitializeAfterSuper(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
