using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AbstractClassPrototypeTests
{
    public const string AbstractSymbolExact = """
        const key = Symbol('abstract-method');
        abstract class Base { [key]() { return 7; } }
        const prototype = Base.prototype;
        console.log(prototype === Object.prototype);
        console.log(Object.getOwnPropertyDescriptor(Object.prototype, key) !== undefined,
            Object.getOwnPropertyDescriptor(prototype, key) !== undefined);
        """;

    public const string DistinctEmptyPrototypes = """
        abstract class First {}
        abstract class Second {}
        const first: any = First.prototype, second: any = Second.prototype;
        console.log(first === Object.prototype, second === Object.prototype, first === second);
        console.log(Object.getPrototypeOf(first) === Object.prototype, first.constructor === First);
        console.log(Object.hasOwn(first, 'constructor'), Object.keys(first).length);
        """;

    public const string InheritanceDescriptors = """
        const key = Symbol('owned');
        let initialized = 0, constructors = 0;
        abstract class Parent {
            field = ++initialized;
            #private = 9;
            constructor() { constructors++; }
            abstract required(): number;
            method() { return this; }
            [key]() { return this; }
            ['computed']() { return this; }
            get observed() { return this; }
            readPrivate() { return this.#private; }
        }
        abstract class Middle extends Parent {}
        class Child extends Middle { required() { return 8; } }
        const base: any = Parent.prototype, middle: any = Middle.prototype;
        const child: any = Child.prototype, ordinary: any = {};
        console.log(initialized, constructors, Object.hasOwn(base, 'field'), Object.hasOwn(base, 'required'));
        console.log(base !== Object.prototype, Object.getPrototypeOf(middle) === base,
            Object.getPrototypeOf(child) === middle, base.constructor === Parent);
        for (const name of ['method', 'computed', key]) {
            const descriptor: any = Object.getOwnPropertyDescriptor(base, name);
            console.log(typeof descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable,
                descriptor.value.call(ordinary) === ordinary, !Object.hasOwn(Object.prototype, name));
        }
        const accessor: any = Object.getOwnPropertyDescriptor(base, 'observed');
        console.log(typeof accessor.get, accessor.enumerable, accessor.configurable, accessor.get.call(ordinary) === ordinary);
        try { base.readPrivate(); } catch (error) { console.log(error instanceof TypeError); }
        const instance: any = new Child();
        console.log(initialized, constructors, instance.field, instance.required(), instance.readPrivate(),
            instance.method() === instance, instance[key]() === instance,
            instance instanceof Parent, instance instanceof Middle, instance instanceof Child);
        """;

    public const string GenericAncestorParentMutation = """
        abstract class Root<T> { inherited(): any { return 'root'; } }
        abstract class Base<T> extends Root<T> { method() { return this; } }
        class Child<T> extends Base<T> { value!: T; }
        const home: any = Base.prototype, root: any = Root.prototype;
        const first: any = new Child<string>(), second: any = new Child<number>();
        console.log(home !== Object.prototype, Object.getPrototypeOf(home) === root,
            first instanceof Base, second instanceof Base, first instanceof Root, second instanceof Root);
        console.log(Object.getOwnPropertyDescriptor(home, 'method')!.value.call(first) === first,
            Object.getOwnPropertyDescriptor(home, 'constructor')!.value === Base);
        Object.setPrototypeOf(home, { inherited: 'changed' });
        console.log(first.inherited, second.inherited, first instanceof Root, second instanceof Root);
        const third: any = new Child<boolean>();
        console.log(third.inherited, third instanceof Base, third instanceof Root);
        console.log(Reflect.setPrototypeOf(home, home));
        Object.setPrototypeOf(home, root);
        console.log(typeof first.inherited, typeof second.inherited, typeof third.inherited,
            first instanceof Root, second instanceof Root, third instanceof Root);
        """;

    public const string LiveComputedDescriptorHeldOwner = """
        const key = 'read';
        abstract class Base { [key]() { return this; } }
        class Target extends Base {}
        class Source extends Target {}
        const Held: typeof Target = Source;
        const instance: any = new Held();
        const prototype: any = Base.prototype;
        console.log(instance.constructor === Source, Object.getPrototypeOf(instance) === Source.prototype,
            instance instanceof Base, prototype !== Object.prototype);
        console.log(instance[key]() === instance, Object.hasOwn(prototype, key));
        let receiver: any;
        prototype[key] = function(this: any) { receiver = this; return 'replacement'; };
        console.log(instance[key](), receiver === instance);
        delete prototype[key];
        console.log(instance[key] === undefined, Object.hasOwn(prototype, key));
        Object.defineProperty(prototype, key, { get: function(this: any) { return this; }, configurable: true });
        console.log(instance[key] === instance, Object.getOwnPropertyDescriptor(prototype, key)!.get!.call(instance) === instance);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["abstract-symbol-exact", AbstractSymbolExact, "false\nfalse true\n"];
        yield return ["distinct-empty-prototypes", DistinctEmptyPrototypes, "false false false\ntrue true\ntrue 0\n"];
        yield return ["inheritance-descriptors-and-initialization", InheritanceDescriptors, "0 0 false false\ntrue true true true\nfunction true false true true true\nfunction true false true true true\nfunction true false true true true\nfunction false true true\ntrue\n1 1 1 8 9 true true true true true\n"];
        yield return ["generic-ancestor-parent-mutation", GenericAncestorParentMutation, "true true true true true true\ntrue true\nchanged changed false false\nchanged true false\nfalse\nfunction function function true true true\n"];
        yield return ["live-computed-descriptor-held-owner", LiveComputedDescriptorHeldOwner, "true true true true\ntrue true\nreplacement true\ntrue false\ntrue true\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            // https://github.com/nickna/SharpTS/issues/2423: interpreter inherited method replacement/deletion.
            if ((string)row[0] != "live-computed-descriptor-held-owner")
                yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void AbstractClassPrototypesHaveIndependentLiveDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
