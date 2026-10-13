using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassAccessorDescriptorTests
{
    internal const string IssueSource = """
        class C {
            get x() { return 1; }
            set y(value: number) {}
            static get staticX() { return 2; }
            static set staticY(value: number) {}
        }
        console.log(Object.getOwnPropertyDescriptor(C.prototype, 'x') === undefined,
            Object.getOwnPropertyDescriptor(C.prototype, 'y') === undefined,
            Object.getOwnPropertyDescriptor(C, 'staticX') === undefined,
            Object.getOwnPropertyDescriptor(C, 'staticY') === undefined);
        """;

    internal const string ShapeSource = """
        let reads = 0, writes = 0;
        class C {
            get read() { reads++; return 1; }
            set write(value: any) { writes++; }
            get pair() { reads++; return 1; }
            set pair(value: any) { writes++; }
            static get read() { reads++; return 1; }
            static set write(value: any) { writes++; }
            static get pair() { reads++; return 1; }
            static set pair(value: any) { writes++; }
        }
        const targets: any[] = [[C.prototype, "read"], [C.prototype, "write"],
            [C.prototype, "pair"], [C, "read"], [C, "write"], [C, "pair"]];
        for (const entry of targets) {
            const descriptor: any = Object.getOwnPropertyDescriptor(entry[0], entry[1]);
            const again: any = Object.getOwnPropertyDescriptor(entry[0], entry[1]);
            console.log(typeof descriptor.get, typeof descriptor.set,
                descriptor.enumerable, descriptor.configurable,
                'value' in descriptor, 'writable' in descriptor,
                descriptor.get === again.get, descriptor.set === again.set,
                Object.hasOwn(entry[0], entry[1]));
            for (const method of [descriptor.get, descriptor.set]) {
                if (method !== undefined) {
                    console.log('prototype' in method,
                        Object.getOwnPropertyDescriptor(method, "prototype") === undefined);
                    try { new method(); console.log(false); }
                    catch (error) { console.log(error instanceof TypeError); }
                    try { Reflect.construct(method, []); console.log(false); }
                    catch (error) { console.log(error instanceof TypeError); }
                }
            }
        }
        console.log(reads, writes);
        """;

    internal static string ShapeExpected
    {
        get
        {
            const string callable = "false true\ntrue\ntrue\n";
            var block = "function undefined false true false false true true true\n" + callable
                + "undefined function false true false false true true true\n" + callable
                + "function function false true false false true true true\n" + callable + callable;
            return block + block + "0 0\n";
        }
    }

    internal const string KeySource = """
        const stringKey = "computed key", symbolKey = Symbol("computed");
        class C {
            get UPPER() { const receiver: any = this; return receiver.slot; }
            set UPPER(value: any) { const receiver: any = this; receiver.slot = value; }
            get "two words"() { const receiver: any = this; return receiver.slot; }
            set "two words"(value: any) { const receiver: any = this; receiver.slot = value; }
            get "default"() { const receiver: any = this; return receiver.slot; }
            set "default"(value: any) { const receiver: any = this; receiver.slot = value; }
            get [stringKey]() { const receiver: any = this; return receiver.slot; }
            set [stringKey](value: any) { const receiver: any = this; receiver.slot = value; }
            get [symbolKey]() { const receiver: any = this; return receiver.slot; }
            set [symbolKey](value: any) { const receiver: any = this; receiver.slot = value; }
            static get UPPER() { const receiver: any = this; return receiver.slot; }
            static set UPPER(value: any) { const receiver: any = this; receiver.slot = value; }
            static get "two words"() { const receiver: any = this; return receiver.slot; }
            static set "two words"(value: any) { const receiver: any = this; receiver.slot = value; }
            static get "default"() { const receiver: any = this; return receiver.slot; }
            static set "default"(value: any) { const receiver: any = this; receiver.slot = value; }
            static get [stringKey]() { const receiver: any = this; return receiver.slot; }
            static set [stringKey](value: any) { const receiver: any = this; receiver.slot = value; }
            static get [symbolKey]() { const receiver: any = this; return receiver.slot; }
            static set [symbolKey](value: any) { const receiver: any = this; receiver.slot = value; }
        }
        const E: any = class Named {
            get UPPER() { const receiver: any = this; return receiver.slot; }
            set UPPER(value: any) { const receiver: any = this; receiver.slot = value; }
            get "two words"() { const receiver: any = this; return receiver.slot; }
            set "two words"(value: any) { const receiver: any = this; receiver.slot = value; }
            get "default"() { const receiver: any = this; return receiver.slot; }
            set "default"(value: any) { const receiver: any = this; receiver.slot = value; }
            get [stringKey]() { const receiver: any = this; return receiver.slot; }
            set [stringKey](value: any) { const receiver: any = this; receiver.slot = value; }
            get [symbolKey]() { const receiver: any = this; return receiver.slot; }
            set [symbolKey](value: any) { const receiver: any = this; receiver.slot = value; }
            static get UPPER() { const receiver: any = this; return receiver.slot; }
            static set UPPER(value: any) { const receiver: any = this; receiver.slot = value; }
            static get "two words"() { const receiver: any = this; return receiver.slot; }
            static set "two words"(value: any) { const receiver: any = this; receiver.slot = value; }
            static get "default"() { const receiver: any = this; return receiver.slot; }
            static set "default"(value: any) { const receiver: any = this; receiver.slot = value; }
            static get [stringKey]() { const receiver: any = this; return receiver.slot; }
            static set [stringKey](value: any) { const receiver: any = this; receiver.slot = value; }
            static get [symbolKey]() { const receiver: any = this; return receiver.slot; }
            static set [symbolKey](value: any) { const receiver: any = this; receiver.slot = value; }
        };
        for (const target of [C.prototype, C, E.prototype, E]) {
            for (const key of ["UPPER", "two words", "default", stringKey, symbolKey]) {
                const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
                const again: any = Object.getOwnPropertyDescriptor(target, key);
                console.log(descriptor.get === again.get, descriptor.set === again.set,
                    Object.hasOwn(target, key), typeof descriptor.get === "function",
                    typeof descriptor.set === "function");
                const receiver: any = { slot: 4 };
                const before = descriptor.get.call(receiver);
                const result = descriptor.set.call(receiver, 9);
                console.log(before, receiver.slot, descriptor.get.bind(receiver)(), result === undefined);
            }
            console.log(Object.getOwnPropertyDescriptor(target, "upper") === undefined,
                Object.getOwnPropertyDescriptor(target, "Upper") === undefined);
        }
        """;

    internal static string KeyExpected => string.Concat(Enumerable.Repeat(
        string.Concat(Enumerable.Repeat("true true true true true\n4 9 9 true\n", 5)) + "true true\n", 4));

    internal const string CaseSensitiveSource = """
        class C {
            get foo() { return 'lower'; }
            get Foo() { return 'upper'; }
        }
        const lower: any = Object.getOwnPropertyDescriptor(C.prototype, 'foo');
        const upper: any = Object.getOwnPropertyDescriptor(C.prototype, 'Foo');
        const instance = new C();
        console.log(lower.get === upper.get, lower.get.call(instance), upper.get.call(instance),
            lower.get.name, upper.get.name);
        """;

    internal const string GenericSource = """
        class Box<T> {
            value: T;
            constructor(value: T) { this.value = value; }
            get item(): T { return this.value; }
            set item(value: T) { this.value = value; }
            static slot: any = 2;
            static get shared() { return (this as any).slot; }
            static set shared(value: any) { (this as any).slot = value; }
            static get replacement() { return (this as any).slot; }
            static set replacement(value: any) { (this as any).slot = value; }
        }
        const owner: any = Box;
        const instance: any = Object.getOwnPropertyDescriptor(owner.prototype, "item");
        const statics: any = Object.getOwnPropertyDescriptor(owner, "shared");
        console.log(typeof instance.get, typeof instance.set, typeof statics.get, typeof statics.set);
        const first: any = new Box<number>(3), second: any = new Box<string>("four");
        const afterInstance: any = Object.getOwnPropertyDescriptor(owner.prototype, "item");
        const afterStatic: any = Object.getOwnPropertyDescriptor(owner, "shared");
        console.log(instance.get === afterInstance.get, instance.set === afterInstance.set,
            statics.get === afterStatic.get, statics.set === afterStatic.set);
        console.log(instance.get.call(first), instance.get.call(second));
        instance.set.call(first, 5);
        instance.set.call(second, "six");
        console.log(first.value, second.value);
        const receiver: any = { slot: 4 };
        const before = statics.get.call(receiver);
        const result = statics.set.call(receiver, 9);
        console.log(before, receiver.slot, statics.get.call(receiver), result === undefined);
        console.log(delete owner.shared,
            Object.getOwnPropertyDescriptor(owner, "shared") === undefined);
        Object.defineProperty(owner, "replacement", {
            value: "redefined", writable: true, configurable: true });
        const changed: any = Object.getOwnPropertyDescriptor(owner, "replacement");
        console.log(changed.value, changed.get === undefined);
        const third: any = new Box<boolean>(true);
        const afterMutation: any = Object.getOwnPropertyDescriptor(owner, "replacement");
        console.log(Object.getOwnPropertyDescriptor(owner, "shared") === undefined,
            Object.hasOwn(owner, "shared"), afterMutation.value,
            afterMutation.get === undefined, third.value);
        """;

    internal const string GenericExpected = "function function function function\ntrue true true true\n"
        + "3 four\n5 six\n4 9 9 true\ntrue true\nredefined true\ntrue false redefined true true\n";

    internal const string ConstrainedGenericSource = """
        const key = Symbol("shared");
        class Base { marker = "base"; }
        class Derived extends Base { marker = "derived"; }
        class Box<T extends Base, U extends T> {
            value: T;
            constructor(value: T) { this.value = value; }
            get item(): T { return this.value; }
            static slot = 5;
            static get shared() { return Box.slot; }
            static get replacement() { return 9; }
            static get [key]() { return (this as any).slot; }
        }
        const owner: any = Box;
        const named: any = Object.getOwnPropertyDescriptor(owner, "shared");
        const symbol: any = Object.getOwnPropertyDescriptor(owner, key);
        const item: any = Object.getOwnPropertyDescriptor(owner.prototype, "item");
        const first: any = new Box<Base, Base>(new Base());
        const second: any = new Box<Derived, Derived>(new Derived());
        console.log(named.get.call(owner), symbol.get.call({ slot: 7 }),
            item.get.call(first).marker, item.get.call(second).marker);
        const namedAgain: any = Object.getOwnPropertyDescriptor(owner, "shared");
        const symbolAgain: any = Object.getOwnPropertyDescriptor(owner, key);
        console.log(named.get === namedAgain.get, symbol.get === symbolAgain.get);
        console.log(delete owner.shared, delete owner[key]);
        Object.defineProperty(owner, "replacement", { value: "changed", configurable: true });
        const third: any = new Box<Base, Derived>(new Base());
        const changed: any = Object.getOwnPropertyDescriptor(owner, "replacement");
        console.log(third.value.marker,
            Object.getOwnPropertyDescriptor(owner, "shared") === undefined,
            Object.getOwnPropertyDescriptor(owner, key) === undefined,
            changed.value);
        """;

    internal const string ConstrainedGenericExpected = "5 7 base derived\ntrue true\ntrue true\nbase true true changed\n";

    internal const string ForwardConstrainedGenericSource = """
        class Base { marker = 3; }
        class Derived extends Base { marker = 4; }
        class Box<T extends U, U extends Base> {
            value: T;
            constructor(value: T) { this.value = value; }
            static slot = 6;
            static get shared() { return Box.slot; }
        }
        const owner: any = Box;
        const descriptor: any = Object.getOwnPropertyDescriptor(owner, "shared");
        const first = new Box<Base, Base>(new Base());
        const again: any = Object.getOwnPropertyDescriptor(owner, "shared");
        console.log(first.value.marker, descriptor.get.call(owner),
            descriptor.get === again.get);
        console.log(delete owner.shared);
        const second = new Box<Derived, Derived>(new Derived());
        console.log(second.value.marker, Object.getOwnPropertyDescriptor(owner, "shared") === undefined);
        class TextBox<T extends string> {
            value: T;
            constructor(value: T) { this.value = value; }
            static get shared() { return "text"; }
        }
        const textOwner: any = TextBox;
        const text = new TextBox<string>("ok");
        const textDescriptor: any = Object.getOwnPropertyDescriptor(textOwner, "shared");
        console.log(text.value, textDescriptor.get.call(textOwner));
        """;

    internal const string ForwardConstrainedGenericExpected = "3 6 true\ntrue\n4 true\nok text\n";

    internal const string LateDeclaredBoundSource = """
        class Box<T extends Base> {
            value: T;
            constructor(value: T) { this.value = value; }
            static get shared() { return 5; }
            static get replacement() { return 6; }
        }
        class Base { marker = "base"; }
        class Derived extends Base { marker = "derived"; }
        const owner: any = Box;
        const descriptor: any = Object.getOwnPropertyDescriptor(owner, "shared");
        console.log(typeof descriptor.get, descriptor.get.call(owner));
        console.log(delete owner.shared,
            Object.getOwnPropertyDescriptor(owner, "shared") === undefined);
        Object.defineProperty(owner, "replacement", { value: "changed", configurable: true });
        const first = new Box<Base>(new Base());
        const second = new Box<Derived>(new Derived());
        const changed: any = Object.getOwnPropertyDescriptor(owner, "replacement");
        console.log(first.value.marker, second.value.marker,
            Object.getOwnPropertyDescriptor(owner, "shared") === undefined, changed.value);
        """;

    internal const string LateDeclaredBoundExpected = "function 5\ntrue true\nbase derived true changed\n";

    internal const string MutationSource = """
        let reads = 0;
        class C {
            get value() { reads++; return 1; }
            set value(value: any) {}
            static get value() { reads++; return 2; }
            static set value(value: any) {}
        }
        for (const target of [C.prototype, C]) {
            const original: any = Object.getOwnPropertyDescriptor(target, "value");
            console.log(delete (target as any).value,
                Object.getOwnPropertyDescriptor(target, "value") === undefined,
                Object.hasOwn(target, "value"));
            Object.defineProperty(target, "value", { value: 7, writable: true, configurable: true });
            const data: any = Object.getOwnPropertyDescriptor(target, "value");
            console.log(data.value, data.get === undefined, data.set === undefined);
            Object.defineProperty(target, "value", { get: original.get, set: original.set });
            const restored: any = Object.getOwnPropertyDescriptor(target, "value");
            console.log(restored.get === original.get, restored.set === original.set,
                restored.enumerable, restored.configurable, 'value' in restored);
            Object.defineProperty(target, "value", { configurable: false });
            console.log(delete (target as any).value);
            try { Object.defineProperty(target, "value", { value: 9 }); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
        }
        console.log(reads);
        """;

    internal const string MutationExpected = "true true false\n7 true true\ntrue true false true false\nfalse\ntrue\n"
        + "true true false\n7 true true\ntrue true false true false\nfalse\ntrue\n0\n";

    internal const string SymbolMutationSource = """
        const key = Symbol("accessor");
        let reads = 0, writes = 0;
        class C {
            slot: any = 1;
            static slot: any = 2;
            get [key]() { reads++; const receiver: any = this; return receiver.slot; }
            set [key](value: any) { writes++; const receiver: any = this; receiver.slot = value; }
            static get [key]() { reads++; const receiver: any = this; return receiver.slot; }
            static set [key](value: any) { writes++; const receiver: any = this; receiver.slot = value; }
        }
        const instance: any = new C(), ctor: any = C;
        const entries: any[] = [[C.prototype, instance, Object.getOwnPropertyDescriptor(C.prototype, key)],
            [ctor, ctor, Object.getOwnPropertyDescriptor(ctor, key)]];
        console.log(reads, writes);
        for (const entry of entries) {
            const target: any = entry[0], receiver: any = entry[1], original: any = entry[2];
            console.log(typeof original.get, typeof original.set, original.enumerable, original.configurable);
            console.log(delete target[key], Object.getOwnPropertyDescriptor(target, key) === undefined,
                Object.hasOwn(target, key));
            Object.defineProperty(target, key, { value: 7, writable: true, configurable: true });
            target[key] = 8;
            const data: any = Object.getOwnPropertyDescriptor(target, key);
            console.log(data.value, target[key], data.get === undefined);
            Object.defineProperty(target, key, { get: original.get, set: original.set });
            console.log(receiver[key]);
            receiver[key] = 9;
            console.log(receiver.slot, receiver[key]);
            Object.defineProperty(target, key, { configurable: false });
            console.log(delete target[key]);
            try { Object.defineProperty(target, key, { value: 10 }); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
        }
        console.log(reads, writes);
        """;

    internal const string SymbolMutationExpected = "0 0\nfunction function false true\ntrue true false\n8 8 true\n"
        + "1\n9 9\nfalse\ntrue\nfunction function false true\ntrue true false\n8 8 true\n"
        + "2\n9 9\nfalse\ntrue\n4 2\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "false false false false\n"];
        yield return ["shape", ShapeSource, ShapeExpected];
        yield return ["keys", KeySource, KeyExpected];
        yield return ["case-sensitive", CaseSensitiveSource, "false lower upper get foo get Foo\n"];
        yield return ["generic", GenericSource, GenericExpected];
        yield return ["constrained-generic", ConstrainedGenericSource, ConstrainedGenericExpected];
        yield return ["forward-constrained-generic", ForwardConstrainedGenericSource, ForwardConstrainedGenericExpected];
        yield return ["late-declared-bound", LateDeclaredBoundSource, LateDeclaredBoundExpected];
        yield return ["mutation", MutationSource, MutationExpected];
        yield return ["symbol-mutation", SymbolMutationSource, SymbolMutationExpected];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ClassAccessorsExposeStableOwnDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void InheritedAndGenericAccessorsKeepTheirDeclaringOwners(ExecutionMode mode)
    {
        const string source = """
            let reads = 0;
            class Base {
                slot: any = 3;
                static slot: any = 4;
                get value() { reads++; const receiver: any = this; return receiver.slot; }
                set value(value: any) { const receiver: any = this; receiver.slot = value; }
                static get value() { reads++; const receiver: any = this; return receiver.slot; }
                static set value(value: any) { const receiver: any = this; receiver.slot = value; }
            }
            class Child extends Base { slot: any = 7; static slot: any = 8; }
            console.log(Object.getOwnPropertyDescriptor(Child.prototype, "value") === undefined,
                Object.getOwnPropertyDescriptor(Child, "value") === undefined,
                Object.hasOwn(Child.prototype, "value"), Object.hasOwn(Child, "value"), reads);
            const instance: any = new Child(), child: any = Child;
            const instanceDescriptor: any = Object.getOwnPropertyDescriptor(Base.prototype, "value");
            const staticDescriptor: any = Object.getOwnPropertyDescriptor(Base, "value");
            console.log(instanceDescriptor.get.call(instance), staticDescriptor.get.call(child));
            instanceDescriptor.set.call(instance, 9);
            staticDescriptor.set.call(child, 10);
            console.log(instance.slot, child.slot, Base.slot);
            class Box<T> {
                value: T;
                constructor(value: T) { this.value = value; }
                get item(): T { return this.value; }
                set item(value: T) { this.value = value; }
            }
            class GenericChild<T> extends Box<T> {}
            const descriptor: any = Object.getOwnPropertyDescriptor(Box.prototype, "item");
            const first: any = new Box<number>(3), second: any = new Box<string>("four");
            console.log(Object.getOwnPropertyDescriptor(GenericChild.prototype, "item") === undefined,
                descriptor.get === Object.getOwnPropertyDescriptor(Box.prototype, "item").get);
            console.log(descriptor.get.call(first), descriptor.get.call(second));
            descriptor.set.call(first, 5);
            descriptor.set.call(second, "six");
            console.log(first.value, second.value);
            """;
        Assert.Equal("true true false false 0\n7 8\n9 10 4\ntrue true\n3 four\n5 six\n",
            TestHarness.Run(source, mode));
    }

    [Fact]
    public void SetterOnlyOverrideShadowsInheritedGetterInInterpretation()
    {
        const string source = """
            let reads = 0;
            class Base {
                slot = 0;
                get value() { reads++; return this.slot; }
                set value(value: number) { this.slot = value; }
            }
            class Derived extends Base {
                override set value(value: number) { this.slot = value * 2; }
            }
            const instance: any = new Derived();
            instance.value = 10;
            console.log(instance.value, instance.slot, reads);
            const descriptor: any = Object.getOwnPropertyDescriptor(Derived.prototype, "value");
            console.log(descriptor.get === undefined, typeof descriptor.set);
            """;
        Assert.Equal("undefined 20 0\ntrue function\n", TestHarness.Run(source, ExecutionMode.Interpreted));
    }

    [Theory, ModeData]
    public void RepeatedNamedExpressionsOwnIndependentAccessorClosures(ExecutionMode mode)
    {
        const string source = """
            function make(value: any) {
                return class Named {
                    get value() { return value; }
                    set value(next: any) { value = next; }
                    static get value() { return value; }
                    static set value(next: any) { value = next; }
                };
            }
            const first: any = make(1), second: any = make(2);
            for (const targets of [[first.prototype, second.prototype], [first, second]]) {
                const left: any = Object.getOwnPropertyDescriptor(targets[0], "value");
                const right: any = Object.getOwnPropertyDescriptor(targets[1], "value");
                console.log(left.get === right.get, left.set === right.set,
                    left.get === Object.getOwnPropertyDescriptor(targets[0], "value").get);
                console.log(left.get.call({}), right.get.call({}));
                left.set.call({}, 3);
                console.log(left.get.call({}), right.get.call({}));
            }
            """;
        Assert.Equal("false false true\n1 2\n3 2\nfalse false true\n3 2\n3 2\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ModuleLocalAccessorValuesKeepTheirExportedCaptures(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["left.ts"] = "export let current = 1; export class Box { get value() { return current; } set value(next: any) { current = next; } }",
            ["right.ts"] = "export let current = 2; export class Box { get value() { return current; } set value(next: any) { current = next; } }",
            ["main.ts"] = """
                import { Box as Left } from './left';
                import { Box as Right } from './right';
                const left: any = Object.getOwnPropertyDescriptor(Left.prototype, "value");
                const right: any = Object.getOwnPropertyDescriptor(Right.prototype, "value");
                console.log(left.get === right.get, left.set === right.set);
                console.log(left.get.call({}), right.get.call({}));
                left.set.call({}, 7);
                console.log(left.get.call({}), right.get.call({}));
                """
        };
        Assert.Equal("false false\n1 2\n7 2\n", TestHarness.RunModules(files, "main.ts", mode));
    }
}
