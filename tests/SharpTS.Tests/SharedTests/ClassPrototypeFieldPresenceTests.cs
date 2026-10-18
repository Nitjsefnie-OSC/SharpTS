using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassPrototypeFieldPresenceTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class A { value = "instance"; }
            const prototype: any = A.prototype;
            console.log(Object.hasOwn(prototype, "value"), Reflect.get(prototype, "value") === undefined);
            prototype.value = "prototype";
            console.log(Object.hasOwn(prototype, "value"), Reflect.get(prototype, "value"), prototype.value);
            const instance: any = new A();
            console.log(instance.value);
            """, "false true\ntrue prototype prototype\ninstance\n"];
        yield return ["nullish-presence-and-deletion", """
            class C { number = 3; flag = true; text = "instance"; }
            const prototype: any = C.prototype;
            console.log(Object.getOwnPropertyDescriptor(prototype, "number") === undefined,
                Object.hasOwn(prototype, "flag"), prototype.text === undefined);
            prototype.number = undefined; prototype.flag = null;
            const number: any = Object.getOwnPropertyDescriptor(prototype, "number");
            const flag: any = Object.getOwnPropertyDescriptor(prototype, "flag");
            console.log(Object.hasOwn(prototype, "number"), number.value === undefined,
                Object.hasOwn(prototype, "flag"), flag.value === null);
            console.log(delete prototype.number, delete prototype.flag);
            console.log(Object.hasOwn(prototype, "number"), Object.hasOwn(prototype, "flag"),
                Reflect.get(prototype, "number") === undefined, prototype.flag === undefined);
            const instance: any = new C();
            console.log(instance.number, instance.flag, instance.text,
                Object.hasOwn(instance, "number"), Object.hasOwn(instance, "flag"));
            """, "true false true\ntrue true true true\ntrue true\nfalse false true true\n3 true instance true true\n"];
        yield return ["inheritance", """
            class Base { base = 1; method() { return 7; } }
            class Derived extends Base { value = 2; }
            const parent: any = Base.prototype, child: any = Derived.prototype;
            console.log(Object.hasOwn(parent, "base"), Object.hasOwn(child, "base"), Object.hasOwn(child, "value"));
            parent.base = "parent"; child.value = "child";
            console.log(child.base, Reflect.get(child, "base"), child.value,
                Object.hasOwn(child, "base"), Object.hasOwn(child, "value"), child.method());
            const instance: any = new Derived();
            console.log(instance.base, instance.value, instance.method());
            """, "false false false\nparent parent child false true 7\n1 2 7\n"];
        yield return ["inherited-write-descriptors", """
            class Base {
                set value(next: any) { (this as any).received = next; }
                get readonly() { return 5; }
            }
            class Derived extends Base {}
            const parent: any = Base.prototype, child: any = Derived.prototype;
            Object.defineProperty(parent, "blocked", { value: 4, writable: false, configurable: true });
            Derived.prototype.value = 8;
            child.blocked = 9; child.readonly = 9;
            console.log(child.received, Object.hasOwn(child, "value"));
            console.log(child.blocked, Object.hasOwn(child, "blocked"), child.readonly, Object.hasOwn(child, "readonly"));
            Object.defineProperty(child, "owned", { value: 1, writable: true, enumerable: false, configurable: false });
            child.owned = 2;
            const owned: any = Object.getOwnPropertyDescriptor(child, "owned");
            console.log(owned.value, owned.writable, owned.enumerable, owned.configurable);
            """, "8 false\n4 false 5 false\n2 true false false\n"];
        yield return ["reflect-deletion", """
            class C { value = 3; }
            const prototype: any = C.prototype;
            prototype.value = null;
            console.log(Reflect.deleteProperty(prototype, "value"), Object.hasOwn(prototype, "value"),
                prototype.value === undefined);
            """, "true false true\n"];
        yield return ["expression-and-generic", """
            const Expression = class { value = "instance"; method() { return 7; } };
            class Box<T> { value: T; constructor(value: T) { this.value = value; } method() { return this.value; } }
            for (const owner of [Expression, Box]) {
                const prototype: any = (owner as any).prototype;
                console.log(Object.hasOwn(prototype, "value"), prototype.value === undefined, typeof prototype.method);
                prototype.value = null;
                console.log(Object.hasOwn(prototype, "value"), prototype.value === null, typeof prototype.method);
            }
            const expression: any = new Expression(), box: any = new Box<number>(5);
            console.log(expression.value, box.value, box.method(), Object.hasOwn(box, "value"));
            """, "false true function\ntrue true function\nfalse true function\ntrue true function\ninstance 5 5 true\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            // Interpreter Reflect.deleteProperty does not delete class prototype properties (#2561).
            // Interpreter class prototype writes bypass inherited descriptors.
            if ((string)row[0] is not ("reflect-deletion" or "inherited-write-descriptors"))
                yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ClassPrototypesOwnOnlyExplicitlyDefinedDataProperties(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
