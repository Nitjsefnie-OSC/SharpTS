using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DeletedStaticAncestorLookupTests
{
    public const string IssueSource = """
        class Base { static get x() { return (this as any)._x; } }
        class Child extends Base { static get x() { return (this as any)._x + 1; } }
        const base: any = Base, child: any = Child;
        base._x = 3;
        child._x = 7;
        console.log(child.x);
        Object.defineProperty(child, 'x', { value: 99, writable: true, configurable: true });
        console.log(child.x);
        console.log(delete child.x, child.x);
        console.log(delete base.x, base.x);
        """;

    public const string UndeclaredChildSource = """
        class Top { static get x(): any { return (this as any)._x; } }
        class Middle extends Top { static x(): any { return 99; } }
        class Child extends Middle {}
        const child: any = Child, middle: any = Middle, topConstructor: any = Top;
        child._x = 7;
        console.log(child.x());
        delete middle.x;
        console.log(typeof child.x, child.x === 7);
        delete topConstructor.x;
        console.log(child.x === undefined);
        """;

    public const string GenericSource = """
        class Base<T> { static get x() { return (this as any)._x; } }
        class Child extends Base<number> { static get x() { return (this as any)._x + 1; } }
        const base: any = Base, child: any = Child;
        base._x = 3;
        child._x = 7;
        console.log(child.x);
        Object.defineProperty(child, 'x', { value: 99, writable: true, configurable: true });
        console.log(child.x);
        console.log(delete child.x, child.x);
        console.log(delete base.x, base.x);
        class DeepBase<T> { static get x(): any { return (this as any)._x; } }
        class Middle<U> extends DeepBase<U> {}
        class DeepChild extends Middle<number> { static get x(): any { return 99; } }
        const deep: any = DeepChild, deepBase: any = DeepBase;
        deep._x = 11;
        console.log(delete deep.x, deep["x"]);
        Object.defineProperty(deepBase, "x", {
            get: function(this: any) { return this._x * 2; }, configurable: true
        });
        console.log(deep.x);
        delete deepBase.x;
        console.log(deep.x === undefined);
        """;

    public const string DescriptorLifecycleSource = """
        class Base { static get x(): any { return (this as any)._x; } }
        class Child extends Base { static get x(): any { return 99; } }
        const base: any = Base, child: any = Child;
        const state = { reads: 0, sets: 0 };
        child._x = 7;
        delete child.x;
        delete base.x;
        Object.defineProperty(base, "x", {
            get: function(this: any) { state.reads++; return this._x * 2; }, configurable: true
        });
        console.log(child.x, child["x"], state.reads);
        Object.defineProperty(child, "x", { value: undefined, configurable: true });
        console.log(Object.hasOwn(child, "x"), child.x === undefined, state.reads);
        Object.defineProperty(child, "x", { value: null, configurable: true });
        console.log(child.x === null, state.reads);
        Object.defineProperty(child, "x", {
            set: function(value: any) { state.sets++; }, configurable: true
        });
        console.log(child.x === undefined, Object.hasOwn(child, "x"), state.reads, state.sets);
        console.log(delete child.x, child.x, state.reads);
        console.log(delete base.x, child.x === undefined, delete base.x);
        Object.defineProperty(base, "x", { value: 23, configurable: true });
        console.log(child.x, child["x"], Object.hasOwn(child, "x"));
        """;

    public const string GetterAbruptSource = """
        const marker = {}, state = { calls: 0, receiver: false };
        class Base { static get x(): any { state.calls++; state.receiver = this === Child; throw marker; } }
        class Child extends Base { static get x(): any { return 1; } }
        const child: any = Child;
        delete child.x;
        try { console.log(child.x); } catch (error) { console.log(error === marker); }
        console.log(state.calls, state.receiver);
        """;

    public const string LivePrototypeSource = """
        class Base { static get x(): any { return "base"; } }
        class Child extends Base { static get x(): any { return "child"; } }
        const child: any = Child, state = { reads: 0 };
        const replacement: any = {};
        Object.defineProperty(replacement, "x", {
            get: function(this: any) { state.reads++; return this === child ? "live" : "wrong"; },
            configurable: true
        });
        delete child.x;
        Object.setPrototypeOf(child, replacement);
        console.log(child.x, child["x"], state.reads);
        Object.defineProperty(replacement, "x", { value: undefined, configurable: true });
        console.log(child.x === undefined, state.reads);
        Object.setPrototypeOf(child, null);
        console.log(child.x === undefined, Object.getPrototypeOf(child) === null, state.reads);
        Object.setPrototypeOf(child, Base);
        console.log(child.x, state.reads);
        """;

    public const string DeclaredChildSource = """
        class Top { static get x() { return (this as any)._x; } }
        class Middle extends Top { static x(): any { return 99; } }
        class Child extends Middle { static get x(): any { return (this as any)._x + 1; } }
        const child: any = Child, middle: any = Middle, topConstructor: any = Top;
        child._x = 7;
        console.log(child.x);
        delete child.x;
        console.log(child.x());
        delete middle.x;
        console.log(child.x);
        delete topConstructor.x;
        console.log(child.x === undefined);
        """;

    public const string NativeAndComputedSource = """
        const numberConstructor: any = Number;
        const numberPrototype: any = Object.getPrototypeOf(numberConstructor);
        const original = Object.getOwnPropertyDescriptor(numberPrototype, 'name')!;
        try {
            Object.defineProperty(numberPrototype, 'name', {
                get() { return this === numberConstructor ? 'inherited' : 'wrong'; }, configurable: true
            });
            console.log(delete numberConstructor.name, numberConstructor.name);
        } finally { Object.defineProperty(numberPrototype, 'name', original); }
        const key: string = 'x';
        class Base { static [key]() { return 3; } }
        class Child extends Base {}
        const baseConstructor: any = Base, child: any = Child;
        console.log(child.x === Object.getOwnPropertyDescriptor(baseConstructor, 'x')!.value);
        const fn: any = child.x;
        console.log(fn.name, delete fn.name, Object.getOwnPropertyDescriptor(fn, 'name') === undefined);
        """;

    public const string InheritedFieldSource = """
        class Base { static value = 1; }
        class Child extends Base { static get value() { return 2; } }
        const child: any = Child;
        console.log(delete child.value, child.value);
        class GenericBase<T> { static value = 1; }
        class GenericChild extends GenericBase<number> { static get value() { return 2; } }
        const genericChild: any = GenericChild;
        console.log(delete genericChild.value, genericChild.value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "8\n99\ntrue 7\ntrue undefined\n"];
        yield return ["undeclared-child-ancestor-tombstone", UndeclaredChildSource, "99\nnumber true\ntrue\n"];
        yield return ["generic-direct-and-deep-continuation", GenericSource, "8\n99\ntrue 7\ntrue undefined\ntrue 11\n22\ntrue\n"];
        yield return ["descriptor-restoration-and-own-masks", DescriptorLifecycleSource, "14 14 2\ntrue true 2\ntrue 2\ntrue true 2 0\ntrue 14 3\ntrue true true\n23 23 false\n"];
        yield return ["getter-abrupt-receiver", GetterAbruptSource, "true\n1 true\n"];
        yield return ["live-and-null-prototype", LivePrototypeSource, "live live 2\ntrue 2\ntrue true 2\nbase 2\n"];
        yield return ["declared-child-ancestor-method", DeclaredChildSource, "8\n99\n7\ntrue\n"];
        yield return ["native-and-computed-preservation", NativeAndComputedSource, "true inherited\ntrue\nx true true\n"];
        yield return ["ordinary-and-generic-inherited-fields", InheritedFieldSource, "true 1\ntrue 1\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void DeletedStaticSlotsContinueThroughLiveAncestorDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
