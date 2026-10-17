using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InstanceAccessorRuntimeSuperclassTests
{
    public const string IssueSource = """
        const Base: any = class { get value() { return 1; } };
        const Derived: any = class extends Base {};
        console.log(new Derived().value);
        """;

    public const string ControlsSource = """
        const InferredBase = class { get value() { return 1; } };
        const InferredChild: any = class extends InferredBase {};
        console.log(new InferredChild().value);
        const FieldBase: any = class { value = 1; };
        const FieldChild: any = class extends FieldBase {};
        console.log(new FieldChild().value);
        class DeclaredBase { get value() { return 1; } }
        const DeclaredChild: any = class extends DeclaredBase {};
        console.log(new DeclaredChild().value);
        """;

    public const string DeclarationDeclarationSource = """
        const state: any = { gets: 0, sets: 0, good: true, expected: null };
        class Base {
            slot = 2;
            get value() { state.gets++; state.good = state.good && this === state.expected; return this.slot; }
            set value(next: number) { state.sets++; state.good = state.good && this === state.expected; this.slot = next; }
        }
        const Parent: any = Base;
        class Child extends Parent { own = 8; }
        const instance: any = new Child();
        state.expected = instance;
        instance.value = 7;
        const first = instance.value;
        const descriptor: any = Object.getOwnPropertyDescriptor(Parent.prototype, "value");
        const raw: any = { slot: 11 };
        state.expected = raw;
        descriptor.set.call(raw, 13);
        console.log(first, instance.slot, descriptor.get.call(raw), raw.slot, state.good, state.gets, state.sets);
        console.log(Object.getPrototypeOf(instance) === Child.prototype, Object.getPrototypeOf(Child.prototype) === Parent.prototype,
            Object.getPrototypeOf(Child) === Parent, instance.constructor === Child, instance instanceof Parent);
        console.log(descriptor === undefined, descriptor.get === Object.getOwnPropertyDescriptor(Parent.prototype, "value")!.get,
            descriptor.set === Object.getOwnPropertyDescriptor(Parent.prototype, "value")!.set, descriptor.enumerable, descriptor.configurable);
        """;

    public const string DeclarationExpressionSource = """
        const state: any = { gets: 0, sets: 0, good: true, expected: null };
        class Base {
            slot = 2;
            get value() { state.gets++; state.good = state.good && this === state.expected; return this.slot; }
            set value(next: number) { state.sets++; state.good = state.good && this === state.expected; this.slot = next; }
        }
        const Parent: any = Base;
        const Child: any = class extends Parent { own = 8; };
        const instance: any = new Child();
        state.expected = instance;
        instance.value = 7;
        const first = instance.value;
        const descriptor: any = Object.getOwnPropertyDescriptor(Parent.prototype, "value");
        const raw: any = { slot: 11 };
        state.expected = raw;
        descriptor.set.call(raw, 13);
        console.log(first, instance.slot, descriptor.get.call(raw), raw.slot, state.good, state.gets, state.sets);
        console.log(Object.getPrototypeOf(instance) === Child.prototype, Object.getPrototypeOf(Child.prototype) === Parent.prototype,
            Object.getPrototypeOf(Child) === Parent, instance.constructor === Child, instance instanceof Parent);
        console.log(descriptor === undefined, descriptor.get === Object.getOwnPropertyDescriptor(Parent.prototype, "value")!.get,
            descriptor.set === Object.getOwnPropertyDescriptor(Parent.prototype, "value")!.set, descriptor.enumerable, descriptor.configurable);
        """;

    public const string ExpressionDeclarationSource = """
        const state: any = { gets: 0, sets: 0, good: true, expected: null };
        const Parent: any = class {
            slot = 2;
            get value() { state.gets++; state.good = state.good && this === state.expected; return this.slot; }
            set value(next: number) { state.sets++; state.good = state.good && this === state.expected; this.slot = next; }
        };
        class Child extends Parent { own = 8; }
        const instance: any = new Child();
        state.expected = instance;
        instance.value = 7;
        const first = instance.value;
        const descriptor: any = Object.getOwnPropertyDescriptor(Parent.prototype, "value");
        const raw: any = { slot: 11 };
        state.expected = raw;
        descriptor.set.call(raw, 13);
        console.log(first, instance.slot, descriptor.get.call(raw), raw.slot, state.good, state.gets, state.sets);
        console.log(Object.getPrototypeOf(instance) === Child.prototype, Object.getPrototypeOf(Child.prototype) === Parent.prototype,
            Object.getPrototypeOf(Child) === Parent, instance.constructor === Child, instance instanceof Parent);
        console.log(descriptor === undefined, descriptor.get === Object.getOwnPropertyDescriptor(Parent.prototype, "value")!.get,
            descriptor.set === Object.getOwnPropertyDescriptor(Parent.prototype, "value")!.set, descriptor.enumerable, descriptor.configurable);
        """;

    public const string ExpressionExpressionSource = """
        const state: any = { gets: 0, sets: 0, good: true, expected: null };
        const Parent: any = class {
            slot = 2;
            get value() { state.gets++; state.good = state.good && this === state.expected; return this.slot; }
            set value(next: number) { state.sets++; state.good = state.good && this === state.expected; this.slot = next; }
        };
        const Child: any = class extends Parent { own = 8; };
        const instance: any = new Child();
        state.expected = instance;
        instance.value = 7;
        const first = instance.value;
        const descriptor: any = Object.getOwnPropertyDescriptor(Parent.prototype, "value");
        const raw: any = { slot: 11 };
        state.expected = raw;
        descriptor.set.call(raw, 13);
        console.log(first, instance.slot, descriptor.get.call(raw), raw.slot, state.good, state.gets, state.sets);
        console.log(Object.getPrototypeOf(instance) === Child.prototype, Object.getPrototypeOf(Child.prototype) === Parent.prototype,
            Object.getPrototypeOf(Child) === Parent, instance.constructor === Child, instance instanceof Parent);
        console.log(descriptor === undefined, descriptor.get === Object.getOwnPropertyDescriptor(Parent.prototype, "value")!.get,
            descriptor.set === Object.getOwnPropertyDescriptor(Parent.prototype, "value")!.set, descriptor.enumerable, descriptor.configurable);
        """;

    public const string GetterThrowSource = """
        const marker = {};
        const Base: any = class { get value(): number { throw marker; } };
        try {
            const Derived: any = class extends Base {};
            console.log(new Derived().value);
        } catch (error) { console.log(error === marker); }
        """;

    public const string MaskingAndSuperReadSource = """
        const state = { gets: 0 };
        const Base: any = class {
            slot = 1;
            get value() { state.gets++; return this.slot; }
            set value(next: number) { this.slot = next; }
        };
        const Setter: any = class extends Base { set value(next: number) { this.slot = next * 2; } };
        const Child: any = class extends Setter {};
        const masked: any = new Child();
        masked.value = 5;
        console.log(masked.value === undefined, masked.slot, state.gets);
        const Super: any = class extends Base {
            get value() { return super.value + 1; }
            set value(next: number) { this.slot = next + 1; }
        };
        const instance: any = new Super();
        instance.value = 7;
        console.log(instance.value, instance.slot, state.gets);
        Object.defineProperty(instance, "value", { value: 20, writable: true, configurable: true });
        console.log(instance.value, instance.slot, state.gets);
        """;

    public const string ComputedAccessorSource = """
        const state = { keys: 0 };
        function key() { state.keys++; return "value"; }
        const Base: any = class {
            slot = 1;
            get [key()]() { return this.slot; }
            set [key()](next: number) { this.slot = next; }
        };
        const Child: any = class extends Base {};
        const instance: any = new Child(), other: any = { slot: 7 };
        const first: any = Object.getOwnPropertyDescriptor(Base.prototype, "value");
        instance.value = 5;
        first.set.call(other, 9);
        const second: any = Object.getOwnPropertyDescriptor(Base.prototype, "value");
        console.log(instance.value, other.slot, first.get.call(other), state.keys);
        console.log(first.get === second.get, first.set === second.set, first.enumerable, first.configurable);
        """;

    public const string InvalidParentsSource = """
        for (const parent of [1, () => {}]) {
            try { const Derived = class extends (parent as any) {}; console.log(typeof Derived); }
            catch (error) { console.log(error instanceof TypeError); }
        }
        """;

    public const string CapturedDefinitionsSource = """
        const effects = { fields: 0, constructors: 0 };
        function make(label: string) {
            const Parent: any = class {
                slot = (effects.fields++, label);
                owner: any;
                constructor(value: string = label) {
                    effects.constructors++; this.slot = value; this.owner = this;
                }
                get value() { return label + ":" + this.slot; }
                set value(next: string) { this.slot = next; }
            };
            const Child: any = class extends Parent { child = label; };
            return { Parent, Child };
        }
        const left: any = make("L"), right: any = make("R");
        console.log(effects.fields, effects.constructors);
        const L: any = left.Child, R: any = right.Child;
        const first: any = new L(), second: any = new R("argument");
        console.log(effects.fields, effects.constructors, first.value, second.value, first.child, second.child);
        first.value = "updated"; second.value = "other";
        console.log(first.value, second.value, first.owner === first, second.owner === second);
        console.log(left.Parent !== right.Parent, L !== R,
            left.Parent.prototype !== right.Parent.prototype, L.prototype !== R.prototype);
        console.log(Object.getPrototypeOf(first) === L.prototype, Object.getPrototypeOf(second) === R.prototype,
            Object.getPrototypeOf(L.prototype) === left.Parent.prototype, Object.getPrototypeOf(R) === right.Parent);
        const a: any = Object.getOwnPropertyDescriptor(left.Parent.prototype, "value");
        const again: any = Object.getOwnPropertyDescriptor(left.Parent.prototype, "value");
        const b: any = Object.getOwnPropertyDescriptor(right.Parent.prototype, "value");
        console.log(a.get === again.get, a.set === again.set, a.get !== b.get, a.set !== b.set);
        const borrowed: any = { slot: "borrow" };
        console.log(a.get.call(borrowed), b.get.call(borrowed));
        a.set.call(borrowed, "set");
        console.log(borrowed.slot, a.get.call(borrowed), b.get.call(borrowed));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "1\n"];
        yield return ["inferred-field-and-known-declared-controls", ControlsSource, "1\n1\n1\n"];
        var matrix = "7 7 13 13 true 2 2\ntrue true true true true\nfalse true true false true\n";
        yield return ["declared-parent-declared-child", DeclarationDeclarationSource, matrix];
        yield return ["declared-parent-expression-child", DeclarationExpressionSource, matrix];
        yield return ["expression-parent-declared-child", ExpressionDeclarationSource, matrix];
        yield return ["expression-parent-expression-child", ExpressionExpressionSource, matrix];
        yield return ["getter-abrupt-value-identity", GetterThrowSource, "true\n"];
        yield return ["setter-masking-super-read-and-own-data", MaskingAndSuperReadSource, "true 10 0\n9 8 1\n20 8 1\n"];
        yield return ["computed-accessor-key-once", ComputedAccessorSource, "5 9 9 2\ntrue true false true\n"];
        yield return ["invalid-parents-retain-type-errors", InvalidParentsSource, "true\ntrue\n"];
        yield return ["captured-definitions-and-borrowed-accessors", CapturedDefinitionsSource,
            "0 0\n2 2 L:L R:argument L R\nL:updated R:other true true\n"
            + "true true true true\ntrue true true true\ntrue true true true\nL:borrow R:borrow\nset L:set R:set\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void RuntimeParentsPreserveInstanceAccessorsAndActualReceivers(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}