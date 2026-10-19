using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DeclaredStaticFieldDescriptorTests
{
    public const string IssueSource = """
        class Top { static get x() { return (this as any)._x; } }
        class Middle extends Top { static x = 99; }
        class Child extends Middle { static get x() { return (this as any)._x + 1; } }
        const child: any = Child, middle: any = Middle, topConstructor: any = Top;
        child._x = 7;
        const descriptor: any = Object.getOwnPropertyDescriptor(middle, 'x');
        console.log('flags', descriptor.writable, descriptor.enumerable, descriptor.configurable);
        """;

    public const string ValueShapesSource = """
        class Plain {
            static number = 3;
            static nil: any = null;
            static missing: any = undefined;
            static callable = () => 7;
        }
        class Generic<T> {
            static number = 3;
            static nil: any = null;
            static missing: any = undefined;
            static callable = () => 7;
        }
        const Expression = class {
            static number = 3;
            static nil: any = null;
            static missing: any = undefined;
            static callable = () => 7;
        };
        for (const constructor of [Plain, Generic, Expression]) {
            const owner: any = constructor;
            for (const key of ["number", "nil", "missing", "callable"]) {
                const descriptor: any = Object.getOwnPropertyDescriptor(owner, key);
                console.log(key, descriptor !== undefined, descriptor && descriptor.value === owner[key],
                    descriptor && descriptor.writable, descriptor && descriptor.enumerable, descriptor && descriptor.configurable);
            }
        }
        const state = { reads: 0 };
        class Far { static get slot(): any { state.reads++; return "far"; } }
        class NearNull extends Far { static slot: any = null; }
        class NearUndefined<T> extends Far { static slot: any = undefined; }
        class NullChild extends NearNull { static get slot(): any { return "child"; } }
        class UndefinedChild extends NearUndefined<number> { static get slot(): any { return "child"; } }
        class BareNull extends NearNull {}
        class BareUndefined extends NearUndefined<number> {}
        const nullChild: any = NullChild, undefinedChild: any = UndefinedChild;
        console.log(delete nullChild.slot, delete undefinedChild.slot);
        console.log(nullChild.slot === null, undefinedChild.slot === undefined, state.reads);
        console.log((BareNull as any).slot === null, (BareUndefined as any).slot === undefined, state.reads);
        """;

    public const string TypedLiveSource = """
        class Typed {
            static numeric: number = 3;
            static readonly fixed: number = 5;
            static nil: null = null;
            static missing: undefined;
            static callback: () => number = () => 7;
        }
        class Constrained<T extends number, U extends T> {
            static numeric: number = 3;
            static readonly fixed: number = 5;
            static nil: null = null;
            static missing: undefined;
            static callback: () => number = () => 7;
        }
        for (const constructor of [Typed, Constrained]) {
            const owner: any = constructor;
            for (const key of ["numeric", "fixed", "nil", "missing", "callback"]) {
                const descriptor: any = Object.getOwnPropertyDescriptor(owner, key);
                console.log(key, descriptor !== undefined, descriptor && descriptor.value === owner[key],
                    descriptor && descriptor.writable, descriptor && descriptor.enumerable, descriptor && descriptor.configurable);
            }
        }
        Typed.numeric = 9;
        Constrained.numeric = 10;
        console.log(Object.getOwnPropertyDescriptor(Typed, "numeric")!.value, Typed.numeric,
            Object.getOwnPropertyDescriptor(Constrained, "numeric")!.value, Constrained.numeric);
        """;

    public const string AliasCoherenceSource = """
        class Plain { static slot = 0; }
        const plainAlias: any = Plain;
        plainAlias.slot = 42;
        console.log(Plain.slot, plainAlias.slot, Object.getOwnPropertyDescriptor(plainAlias, "slot")!.value);
        Plain.slot = 7;
        console.log(Plain.slot, plainAlias.slot, Object.getOwnPropertyDescriptor(plainAlias, "slot")!.value);
        class Generic<T> { static slot = 1; }
        const genericAlias: any = Generic;
        genericAlias.slot = 43;
        console.log(Generic.slot, genericAlias.slot, Object.getOwnPropertyDescriptor(genericAlias, "slot")!.value);
        Generic.slot = 8;
        console.log(Generic.slot, genericAlias.slot, Object.getOwnPropertyDescriptor(genericAlias, "slot")!.value);
        """;

    public const string DeletionRestorationSource = """
        class C { static value = 3; }
        const owner: any = C;
        console.log(delete owner.value, Object.hasOwn(owner, "value"), owner.value === undefined);
        console.log(Object.getOwnPropertyDescriptor(owner, "value") === undefined);
        Object.defineProperty(owner, "value", { value: 8, writable: true, enumerable: false, configurable: true });
        const descriptor = Object.getOwnPropertyDescriptor(owner, "value")!;
        console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
        console.log(delete owner.value, Object.hasOwn(owner, "value"), owner.value === undefined);
        class Generic<T> { static value = 5; }
        const generic: any = Generic;
        function strictDelete(target: any) { "use strict"; return delete target["value"]; }
        console.log(strictDelete(generic), Object.hasOwn(generic, "value"), generic.value === undefined,
            Object.getOwnPropertyDescriptor(generic, "value") === undefined);
        class Parent { static inherited = 13; }
        class Child extends Parent {}
        const child: any = Child;
        console.log(delete child["inherited"], Object.hasOwn(child, "inherited"), child.inherited, Parent.inherited);
        """;

    public const string ExplicitPrecedenceSource = """
        class C { static value = 3; }
        const owner: any = C, state = { reads: 0 };
        Object.defineProperty(owner, "value", { value: 7, writable: true, enumerable: false, configurable: true });
        let descriptor: any = Object.getOwnPropertyDescriptor(owner, "value");
        console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
        Object.defineProperty(owner, "value", { writable: false });
        descriptor = Object.getOwnPropertyDescriptor(owner, "value");
        console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
        const getter = function(this: any) { state.reads++; return 12; };
        Object.defineProperty(owner, "value", { get: getter, configurable: true });
        descriptor = Object.getOwnPropertyDescriptor(owner, "value");
        console.log(descriptor.get === getter, Object.hasOwn(descriptor, "value"), state.reads);
        console.log(owner.value, state.reads);
        Object.defineProperty(owner, "value", { value: null, configurable: true });
        console.log(Object.getOwnPropertyDescriptor(owner, "value")!.value === null, state.reads);
        Object.defineProperty(owner, "value", { value: undefined, configurable: true });
        console.log(Object.getOwnPropertyDescriptor(owner, "value")!.value === undefined, state.reads);
        """;

    public const string ClassificationSource = """
        const key: string = "computed";
        class Base {
            static value = 3;
            static callback = () => 7;
            static method() { return 4; }
            static [key]() { return this; }
        }
        class Child extends Base {}
        console.log(Object.getOwnPropertyDescriptor(Child, "value") === undefined,
            Object.getOwnPropertyDescriptor(Child, "method") === undefined,
            Object.getOwnPropertyDescriptor(Child, key) === undefined);
        const method = Object.getOwnPropertyDescriptor(Base, "method")!;
        const constant = Object.getOwnPropertyDescriptor(Number, "MAX_VALUE")!;
        console.log(method.writable, method.enumerable, method.configurable, method.value === Base.method);
        console.log(constant.writable, constant.enumerable, constant.configurable);
        const callback = Object.getOwnPropertyDescriptor(Base, "callback")!;
        console.log(callback.writable, callback.enumerable, callback.configurable, callback.value === Base.callback);
        const base: any = Base, child: any = Child, receiver = {};
        const computed = Object.getOwnPropertyDescriptor(base, key)!;
        console.log(computed.value === base[key], computed.value === child[key], computed.value.name,
            computed.value.call(receiver) === receiver, computed.writable, computed.enumerable, computed.configurable);
        """;

    public const string IntegritySource = """
        class Sealed { static value = 3; }
        class Frozen { static value = 4; }
        Object.seal(Sealed);
        Object.freeze(Frozen);
        for (const owner of [Sealed, Frozen]) {
            const descriptor: any = Object.getOwnPropertyDescriptor(owner, "value");
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(Reflect.deleteProperty(owner, "value"));
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "flags true true true\n"];
        yield return ["value-shapes-and-nullish-ancestor-masks", ValueShapesSource, "number true true true true true\nnil true true true true true\nmissing true true true true true\ncallable true true true true true\nnumber true true true true true\nnil true true true true true\nmissing true true true true true\ncallable true true true true true\nnumber true true true true true\nnil true true true true true\nmissing true true true true true\ncallable true true true true true\ntrue true\ntrue true 0\ntrue true 0\n"];
        yield return ["typed-and-constrained-live-fields", TypedLiveSource, "numeric true true true true true\nfixed true true true true true\nnil true true true true true\nmissing true true true true true\ncallback true true true true true\nnumeric true true true true true\nfixed true true true true true\nnil true true true true true\nmissing true true true true true\ncallback true true true true true\n9 9 10 10\n"];
        yield return ["alias-and-direct-write-coherence", AliasCoherenceSource, "42 42 42\n7 7 7\n43 43 43\n8 8 8\n"];
        yield return ["deletion-and-explicit-restoration", DeletionRestorationSource, "true false true\ntrue\n8 true false true\ntrue false true\ntrue false true true\ntrue false 13 13\n"];
        yield return ["explicit-data-partial-and-accessor-precedence", ExplicitPrecedenceSource, "7 true false true\n7 false false true\ntrue false 0\n12 1\ntrue 1\ntrue 1\n"];
        yield return ["field-method-and-native-classification", ClassificationSource, "true true true\ntrue false true true\nfalse false false\ntrue true true true\ntrue true computed true true false true\n"];
        yield return ["sealed-and-frozen-flags", IntegritySource, "3 true true false\nfalse\n4 false true false\nfalse\n"];
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
    public void DeclaredStaticFieldsHaveLiveOrdinaryDataDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
