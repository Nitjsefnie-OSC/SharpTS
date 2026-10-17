using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GeneratorRuntimeSuperclassTests
{
    public const string IssueSource = """
        function make() { return class { *values() { yield 1; } }; }
        const Base = make();
        class Child extends Base {}
        console.log(new Child().values().next().value);
        """;

    public const string ControlsSource = """
        function ordinary() { return class { values() { return 1; } }; }
        const OrdinaryBase = ordinary();
        class OrdinaryChild extends OrdinaryBase {}
        console.log(new OrdinaryChild().values());
        function generator() { return class { *values() { yield 1; } }; }
        const DirectBase = generator();
        console.log(new DirectBase().values().next().value);
        class NominalBase { *values() { yield 1; } }
        class NominalChild extends NominalBase {}
        console.log(new NominalChild().values().next().value);
        const KnownBase = class { *values() { yield 1; } };
        class KnownDeclaredChild extends KnownBase {}
        const KnownExpressionChild = class extends KnownBase {};
        console.log(new KnownDeclaredChild().values().next().value,
            new KnownExpressionChild().values().next().value);
        """;

    public const string CapturesSource = """
        function make(seed: number) {
            return class {
                value = seed;
                *values(): Generator<any, void, unknown> { yield seed; yield this.value; yield this; }
            };
        }
        const First: any = make(7), Second: any = make(11);
        class DeclaredChild extends First { own = 3; }
        const ExpressionChild: any = class extends Second { own = 5; };
        const first: any = new DeclaredChild(), second: any = new ExpressionChild();
        for (const instance of [first, second]) {
            const iterator: any = instance.values();
            console.log(iterator.next().value, iterator.next().value, iterator.next().value === instance,
                iterator.next().done, instance.own);
        }
        console.log(first instanceof First, first instanceof Second, second instanceof First, second instanceof Second);
        console.log(Object.getPrototypeOf(DeclaredChild) === First, Object.getPrototypeOf(ExpressionChild) === Second,
            Object.getPrototypeOf(DeclaredChild.prototype) === First.prototype,
            Object.getPrototypeOf(ExpressionChild.prototype) === Second.prototype);
        """;

    // #2255 separately owns completion checking with explicit Generator annotations.
    public const string ProtocolSource = """
        const state = { closed: 0 }, marker = {};
        function make() {
            return class {
                value = 7;
                *values() {
                    try {
                        const next: number = yield this.value;
                        yield next;
                        return next + 1;
                    } finally { state.closed++; }
                }
            };
        }
        const Parent: any = make();
        class Child extends Parent {}
        const instance: any = new Child(), iterator: any = instance.values();
        const first = iterator.next(), second = iterator.next(40), completed = iterator.next();
        console.log(first.value, first.done, second.value, second.done, completed.value, completed.done, state.closed);
        console.log(iterator.next().done, state.closed);
        const returned: any = instance.values();
        returned.next();
        const result = returned.return(99);
        console.log(result.value, result.done, state.closed);
        const thrown: any = instance.values();
        thrown.next();
        try { thrown.throw(marker); } catch (error) { console.log(error === marker); }
        console.log(state.closed, thrown.next().done);
        """;

    public const string DeclaredInstanceSource = """
        class Base {
            value: number;
            constructor(value = 4, ...rest: number[]) { this.value = value + rest.length; }
            *values(extra = 1): Generator<any, void, unknown> {
                yield this.value + extra;
                yield this;
                yield arguments.length;
            }
        }
        const Parent: any = Base;
        const direct: any = new Parent(5, 8, 9);
        const before: any = direct.values(2);
        console.log(before.next().value, before.next().value === direct, before.next().value);
        class Child extends Parent { child = 3; }
        const ChildConstructor: any = Child;
        const child: any = new ChildConstructor(5, 8, 9), iterator: any = child.values(2);
        console.log(iterator.next().value, iterator.next().value === child, iterator.next().value,
            child.child, child instanceof Parent, child instanceof Child);
        const ExpressionChild: any = class extends Parent {};
        const expression: any = new ExpressionChild();
        console.log(expression.value, expression.values().next().value, expression instanceof Parent);
        """;

    public const string DeclaredStaticContextSource = """
        class Ancestor { static read() { return (this as any).value; } }
        class Base extends Ancestor {
            static value = 7;
            static *values(extra = this.value, ...rest: number[]): Generator<any, void, unknown> {
                const arrow = () => this;
                yield this;
                yield arrow() === this;
                yield extra + rest.length;
                yield arguments.length;
                yield super.read();
            }
        }
        const Parent: any = Base;
        function show(iterator: any, expected: any) {
            console.log(iterator.next().value === expected, iterator.next().value,
                iterator.next().value, iterator.next().value, iterator.next().value);
        }
        show(Parent.values(undefined, 8, 9), Parent);
        class Child extends Parent {}
        show((Child as any).values(undefined, 8, 9), Child);
        const borrowed: any = Object.getOwnPropertyDescriptor(Parent, 'values')!.value;
        const receiver: any = { value: 20 };
        show(borrowed.call(receiver, undefined, 6), receiver);
        """;

    public const string StaticExpressionSource = """
        function make() { return class { static *values(): Generator<any, void, unknown> { yield this; } }; }
        const Base = make();
        class Child extends Base {}
        console.log(Child.values().next().value === Child);
        """;

    public const string PrototypeSource = """
        const Parent: any = class { *values(): Generator<any, void, unknown> { yield this; } };
        class Child extends Parent {}
        const instance: any = new Child();
        const descriptor = Object.getOwnPropertyDescriptor(Parent.prototype, "values")!;
        const method: any = instance.values;
        console.log(method === descriptor.value, method === instance.values,
            descriptor.value === Object.getOwnPropertyDescriptor(Parent.prototype, "values")!.value,
            Object.getOwnPropertyDescriptor(Child.prototype, "values") === undefined);
        console.log(descriptor.writable, descriptor.enumerable, descriptor.configurable,
            Object.getPrototypeOf(method) === Object.getPrototypeOf(function*() {}));
        const original = method.prototype, first = method.call(instance), advance: any = first.next;
        console.log(Object.getPrototypeOf(first) === original, first.next().value === instance);
        const replacement = { marker: 1 };
        method.prototype = replacement;
        const second = method.call(instance);
        console.log(Object.getPrototypeOf(second) === replacement, advance.call(second).value === instance);
        method.prototype = original;
        console.log(Object.getPrototypeOf(method.call(instance)) === original, descriptor.value === instance.values);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "1\n"];
        yield return ["representation-controls", ControlsSource, "1\n1\n1\n1 1\n"];
        yield return ["captured-fields-and-definitions", CapturesSource, "7 7 true true 3\n11 11 true true 5\ntrue false false true\ntrue true true true\n"];
        yield return ["next-completion-close-and-marker", ProtocolSource, "7 false 40 false 41 true 1\ntrue 1\n99 true 2\ntrue\n3 true\n"];
        yield return ["declared-instance-both-child-forms", DeclaredInstanceSource, "9 true 1\n9 true 1 3 true true\n4 5 true\n"];
        yield return ["declared-static-context", DeclaredStaticContextSource, "true true 9 3 7\ntrue true 9 3 7\ntrue true 21 2 20\n"];
        yield return ["static-generator-only", StaticExpressionSource, "true\n"];
        yield return ["descriptor-and-iterator-prototype", PrototypeSource, "true true true true\ntrue false true true\ntrue true\ntrue true\ntrue true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void RuntimeGeneratorParentsPreserveInitializationContextAndProtocol(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
