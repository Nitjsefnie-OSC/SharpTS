using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ReflectClassStaticPropertyTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class C { static data = 5; static get value() { return 7; } }
            console.log(C.data, C.value, Reflect.get(C, "data"), Reflect.get(C, "value"));
            """, "5 7 5 7\n"];
        yield return ["receivers", """
            class Base { static slot = 3; static get value() { return this.slot; } static get owner() { return this; } }
            class Child extends Base { static slot = 9; }
            const receiver = { slot: 17 };
            console.log(Reflect.get(Base, "value"), Reflect.get(Child, "value"), Reflect.get(Child, "value", receiver));
            console.log(Reflect.get(Base, "slot", receiver), Reflect.get(Child, "slot", receiver));
            console.log(Reflect.get(Child, "owner") === Child, Reflect.get(Child, "owner", receiver) === receiver,
                Reflect.get(Child, "owner", undefined) === undefined, Reflect.get(Child, "owner", null) === null);
            """, "3 9 17\n3 9\ntrue true true true\n"];
        yield return ["nullish-shadowing-and-deletion", """
            let reads = 0;
            class Base { static data: any = 11; static get value() { reads++; return 7; } }
            class Child extends Base { static data: any = undefined; static set value(next: any) {} }
            const owner: any = Child;
            console.log(Reflect.get(owner, "data") === undefined, Reflect.get(owner, "value") === undefined, reads);
            Object.defineProperty(owner, "data", { value: null, configurable: true });
            console.log(Reflect.get(owner, "data") === null);
            delete owner.data;
            console.log(Reflect.get(owner, "data"));
            Object.defineProperty(owner, "value", { value: undefined, configurable: true });
            console.log(Reflect.get(owner, "value") === undefined, reads);
            delete owner.value;
            console.log(Reflect.get(owner, "value"), reads);
            """, "true true 0\ntrue\n11\ntrue 0\n7 1\n"];
        yield return ["prototype-overrides", """
            class Base { static data = 3; }
            class C extends Base { static slot = 9; }
            const custom: any = { data: 12, slot: 17 };
            Object.defineProperty(custom, "value", {
                get: function(this: any) { return this.slot; }, configurable: true
            });
            Object.setPrototypeOf(C, custom);
            console.log(Reflect.get(C, "data"), Reflect.get(C, "value"), Reflect.get(C, "value", { slot: 21 }),
                Reflect.get(C, "slot"));
            """, "12 9 21 9\n"];
        yield return ["expression-generic-and-metadata", """
            const Expression = class Named { static data = 5; static get value() { return 7; } };
            class Generic<T> { static data = 11; static get value() { return 13; } }
            for (const owner of [Expression, Generic]) {
                const constructor: any = owner;
                console.log(Reflect.get(constructor, "data"), Reflect.get(constructor, "value"));
                console.log(Reflect.get(constructor, "length") === constructor.length,
                    Reflect.get(constructor, "prototype") === constructor.prototype,
                    Reflect.get(constructor, "missing") === undefined);
            }
            """, "5 7\ntrue true true\n11 13\ntrue true true\n"];
        yield return ["name-metadata", """
            class C {}
            const owner: any = C, descriptor: any = Object.getOwnPropertyDescriptor(owner, "name");
            console.log(JSON.stringify([owner.name, Reflect.get(owner, "name"), descriptor.value]));
            """, "[\"C\",\"C\",\"C\"]\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            // Interpreter constructor prototype mutation (#2443) remains separate.
            if ((string)row[0] is not "prototype-overrides")
                yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ReflectGetReadsStaticClassPropertiesWithTheirOriginalReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
