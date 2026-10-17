using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticAccessorRuntimeSuperclassTests
{
    public const string IssueSource = """
        const effects = { gets: 0 };
        const Base: any = class {
            static get [Symbol.iterator]() { effects.gets++; return undefined; }
        };
        const Derived: any = class extends Base {};
        console.log(new Derived() instanceof Base, effects.gets);
        const Plain: any = class {}, Control: any = class extends Plain {};
        console.log(new Control() instanceof Plain);
        """;

    public const string NamedAccessorsSource = """
        const events: any = { fields: 0, constructors: 0, gets: 0, sets: 0, valid: true, expected: null };
        const Base: any = class {
            value = (events.fields++, 7);
            constructor(value: any) { events.constructors++; this.value = value; }
            static get named() {
                events.gets++; events.valid = events.valid && this === events.expected;
                return (this as any).slot;
            }
            static set named(value: any) {
                events.sets++; events.valid = events.valid && this === events.expected;
                (this as any).slot = value;
            }
        };
        const Derived: any = class extends Base { child = 8; };
        events.expected = Derived;
        console.log(events.fields, events.constructors, events.gets, events.sets);
        const value: any = new Derived(9);
        console.log(value.value, value.child, events.fields, events.constructors, events.gets, events.sets);
        // Inherited evaluated-constructor getter receivers are tracked by #2324.
        // Keep this getter control on its own constructor and exercise the inherited setter.
        events.expected = Base;
        Base.slot = 10;
        console.log(Base.named);
        events.expected = Derived;
        Derived.named = 11;
        console.log(Derived.slot, Base.slot, events.valid, events.gets, events.sets);
        """;

    // Direct member construction's class-expression leaf-name collision is tracked in #2249.
    public const string EvaluatedDefinitionsSource = """
        function make(tag: any) {
            const Parent: any = class {
                value = tag;
                static get label() { return tag; }
            };
            const Child: any = class extends Parent {};
            return { Parent, Child };
        }
        const left: any = make("left"), right: any = make("right");
        const L: any = left.Child, R: any = right.Child;
        const a: any = new L(), b: any = new R();
        console.log(a.value, b.value, left.Parent.label, right.Child.label);
        console.log(left.Parent !== right.Parent, left.Child !== right.Child,
            left.Parent.prototype !== right.Parent.prototype, left.Child.prototype !== right.Child.prototype);
        console.log(Object.getPrototypeOf(left.Child) === left.Parent,
            Object.getPrototypeOf(left.Child.prototype) === left.Parent.prototype);
        console.log(Object.getPrototypeOf(a) === left.Child.prototype, Object.getPrototypeOf(b) === right.Child.prototype);
        console.log(a instanceof left.Parent, a instanceof right.Parent, b instanceof right.Parent);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true 0\ntrue\n"];
        yield return ["named-accessors", NamedAccessorsSource, "0 0 0 0\n9 8 1 1 0 0\n10\n11 10 true 1 1\n"];
        yield return ["evaluated-definitions", EvaluatedDefinitionsSource,
            "left right left right\ntrue true true true\ntrue true\ntrue true\ntrue false true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void StaticAccessorsPreserveRuntimeSuperclassInitializationAndIdentity(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
