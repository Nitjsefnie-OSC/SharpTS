using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticGetterReceiverTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class C { static get x() { return (this as any)._x; } }
            const owner: any = C;
            owner._x = 3;
            console.log(C.x);
            """, "3\n"];
        yield return ["receivers", """
            class Base {
                static get owner(): any { return this; }
                static get value(): any { return (this as any).slot; }
                static read(): any { return this.value; }
            }
            class Derived extends Base {}
            const base: any = Base, derived: any = Derived;
            base.slot = 3;
            derived.slot = 7;
            console.log(Base.owner === Base, Derived.owner === Derived);
            console.log(Base.value, Derived.value, base.value, derived["value"]);
            console.log(Base.read(), Derived.read());
            const getter: any = Object.getOwnPropertyDescriptor(Base, "value")!.get;
            console.log(getter.call({ slot: 11 }));
            """, "true true\n3 7 3 7\n3 7\n11\n"];
        yield return ["nested-capture", """
            class Other { static get value(): any { return (this as any).slot; } }
            class C {
                static get value(): any {
                    const before = this;
                    const other = Other.value;
                    console.log(before === this);
                    return (this as any).slot + other;
                }
                static get arrow(): any { return () => this; }
                static get fail(): any { throw new Error("boom"); }
                static after(): any { try { const ignored = this.fail; } catch (error) {} return this; }
            }
            (Other as any).slot = 5;
            (C as any).slot = 3;
            console.log(1 + C.value);
            const arrow = C.arrow;
            console.log(Other.value, arrow() === C, C.after() === C);
            """, "true\n9\n5 true true\n"];
        yield return ["typed-and-replaced", """
            class C {
                static get number(): number { return Number((this as any).slot); }
                static get flag(): boolean { return !!(this as any).slot; }
                static get text(): string { return String((this as any).slot); }
                static get missing(): any { return undefined; }
                static get nil(): any { return null; }
            }
            (C as any).slot = 3;
            console.log(C.number + 1, C.flag, C.text, C.missing === undefined, C.nil === null);
            Object.defineProperty(C, "number", {
                get: function() { return this === C ? 7 : 9; }, configurable: true
            });
            console.log(C.number, 1 + C.number);
            """, "4 true 3 true true\n7 8\n"];
        yield return ["generic", """
            class Base<T> {
                static get value(): any { return (this as any).slot; }
                static read(): any { return this.value; }
            }
            class Derived<T> extends Base<T> {}
            const base: any = Base, derived: any = Derived;
            base.slot = 2;
            derived.slot = 8;
            console.log(Base.value, Derived.value, base.value, derived.value);
            console.log(Base.read(), Derived.read());
            """, "2 8 2 8\n2 8\n"];
        yield return ["initialization", """
            class C {
                static _value = 3;
                static get x(): any { return (this as any)._value; }
                static first = C.x;
                static { console.log(this.x, this.first); }
            }
            console.log(C.first, C.x);
            """, "3 3\n3 3\n"];
        yield return ["abstract-and-computed", """
            abstract class Base { static get value(): any { return (this as any).slot; } }
            class Derived extends Base {}
            (Base as any).slot = 4;
            (Derived as any).slot = 6;
            console.log(Base.value, Derived.value);
            class C {
                static slot = 9;
                static get ["x"](): any { return (this as any).slot; }
                static first = C.x;
            }
            console.log(C.first, C.x);
            const getter: any = Object.getOwnPropertyDescriptor(C, "x")!.get;
            console.log(getter.name, getter.length, getter === Object.getOwnPropertyDescriptor(C, "x")!.get);
            """, "4 6\n9 9\nget x 0 true\n"];
    }

    public const string ModuleSource = """
        class C {
            static get x(): any { return (this as any).slot; }
        }
        export = C;
        """;
    public const string ModuleEntry = """
        import Imported = require('./owner');
        (Imported as any).slot = 19;
        console.log(Imported.x, (Imported as any)["x"]);
        """;

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void SourceStaticGettersUseActualConstructor(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ImportedClassAliasPreservesReceiver(ExecutionMode mode)
        => Assert.Equal("19 19\n", TestHarness.RunModules(new Dictionary<string, string>
        {
            ["owner.ts"] = ModuleSource,
            ["main.ts"] = ModuleEntry
        }, "main.ts", mode));
}
