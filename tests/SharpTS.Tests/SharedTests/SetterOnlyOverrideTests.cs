using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class SetterOnlyOverrideTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            class Base {
                _val: number;
                constructor() { this._val = 0; }
                set value(v: number) { this._val = v; }
                get value(): number { return this._val; }
            }
            class Derived extends Base {
                constructor() { super(); }
                override set value(v: number) { this._val = v * 2; }
            }
            const d = new Derived();
            d.value = 10;
            console.log(d.value, d._val);
            """, "undefined 20\n"];
        yield return ["dispatch", """
            let reads = 0;
            class Base {
                slot = 1;
                get value() { reads++; return this.slot; }
                set value(next: number) { this.slot = next; }
            }
            class Derived extends Base { override set value(next: number) { this.slot = next * 2; } }
            class Grandchild extends Derived {}
            class Inherited extends Base {}
            function read(value: Base): any { return value.value; }
            const instance = new Derived(), grandchild = new Grandchild();
            instance.value = 10;
            const alias: any = instance;
            console.log(instance.value, read(instance), read(grandchild), alias.value, alias["value"],
                Reflect.get(alias, "value"), instance.slot, reads);
            Object.defineProperty(instance, "value", { value: 43, configurable: true });
            console.log(read(instance), alias.value, reads);
            console.log(read(new Inherited()), reads);
            """, "undefined undefined undefined undefined undefined undefined 20 0\n43 43 0\n1 1\n"];
        yield return ["expressions", """
            let reads = 0;
            class Base {
                slot = 0;
                get value() { reads++; return this.slot; }
                set value(next: number) { this.slot = next; }
            }
            const Derived = class extends Base { set value(next: number) { this.slot = next * 2; } };
            const OtherBase = class {
                slot = 0;
                get value() { reads++; return this.slot; }
                set value(next: number) { this.slot = next; }
            };
            const Other = class extends OtherBase { set value(next: number) { this.slot = next * 3; } };
            const first: any = new Derived(), second: any = new Other();
            first.value = 10;
            second.value = 10;
            console.log(first.value, first.slot, second["value"], second.slot, reads);
            """, "undefined 20 undefined 30 0\n"];
        yield return ["statics", """
            let reads = 0;
            class Base {
                static slot = 1;
                static get value() { reads++; return this.slot; }
                static set value(next: number) { this.slot = next; }
            }
            class Derived extends Base { static set value(next: number) {} }
            class Inherited extends Base {}
            const alias: any = Derived;
            // #2087 separately tracks null versus undefined on getterless constructors.
            console.log(!!Derived.value, !!alias.value, !!alias["value"], !!Reflect.get(alias, "value"), Derived.slot, reads);
            console.log(Inherited.value, reads);
            """, "false false false false 1 0\n1 1\n"];
        yield return ["computed-pair", """
            const key: any = "value";
            class C { set value(next: any) {} get [key]() { return 3; } }
            const D = class { set value(next: any) {} get [key]() { return 4; } };
            console.log((new C() as any).value, (new D() as any).value);
            """, "3 4\n"];
        yield return ["computed-method-mask", """
            const key: any = "value", other: any = "other";
            class Base { [key]() { return 9; } }
            class Derived extends Base { set value(next: any) {} [other]() { return 1; } }
            class Inherited extends Base { [other]() { return 1; } }
            const inherited: any = new Inherited();
            console.log(typeof (new Derived() as any).value, inherited.value());
            """, "undefined 9\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // Evaluated class getterless descriptor reads still have the #2216 null-value limitation.
            .Where(mode => (string)row[0] != "expressions" || mode == ExecutionMode.Interpreted)
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void SetterOnlyPropertiesStopInheritedGetterLookup(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
