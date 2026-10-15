using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassSymbolAccessorNameTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            const anonymous = Symbol(), described = Symbol("label"), empty = Symbol("");
            const text = "text";
            class C {
                get [anonymous]() { return 0; } set [anonymous](value: any) {}
                get [described]() { return 0; } set [described](value: any) {}
                get [empty]() { return 0; } set [empty](value: any) {}
                get [text]() { return 0; } set [text](value: any) {}
                static get [anonymous]() { return 0; } static set [anonymous](value: any) {}
                static get [described]() { return 0; } static set [described](value: any) {}
                static get [empty]() { return 0; } static set [empty](value: any) {}
                static get [text]() { return 0; } static set [text](value: any) {}
            }
            const constructor: any = C;
            for (const owner of [C.prototype, constructor]) {
                for (const key of [anonymous, described, empty, text]) {
                    const descriptor = Object.getOwnPropertyDescriptor(owner, key)!;
                    console.log(JSON.stringify(descriptor.get!.name), JSON.stringify(descriptor.set!.name));
                }
            }
            """, string.Concat(Enumerable.Repeat("\"get \" \"set \"\n\"get [label]\" \"set [label]\"\n\"get []\" \"set []\"\n\"get text\" \"set text\"\n", 2))];
        yield return ["expressions", """
            const key = Symbol("label");
            function make(value: number) {
                return class {
                    get [key]() { return this.saved; } set [key](next: any) { this.saved = next; }
                    saved: any;
                    static get [key]() { return value; } static set [key](next: any) {}
                };
            }
            const A = make(41), B = make(42);
            for (const owner of [A.prototype, A, B.prototype, B]) {
                const descriptor = Object.getOwnPropertyDescriptor(owner, key)!;
                const getter: any = descriptor.get, setter: any = descriptor.set;
                console.log(getter.name, setter.name, getter.call({ saved: 45 }));
            }
            const original = Object.getOwnPropertyDescriptor(A.prototype, key)!;
            Object.defineProperty(original.get, "name", { value: "changed" });
            delete (original.set as any).name;
            const again = Object.getOwnPropertyDescriptor(A.prototype, key)!;
            console.log(again.get === original.get, again.set === original.set, again.get!.name, Object.hasOwn(again.set, "name"));
            console.log(Object.getOwnPropertyDescriptor(A.prototype, key)!.get
                === Object.getOwnPropertyDescriptor(B.prototype, key)!.get);
            """, "get [label] set [label] 45\nget [label] set [label] 41\n"
                + "get [label] set [label] 45\nget [label] set [label] 42\ntrue true changed false\nfalse\n"];
        yield return ["lifecycle", """
            const key = Symbol("label");
            class C<T> {
                value = 41;
                get [key]() { return this.value; }
                set [key](value: number) { this.value = value; }
            }
            new C<number>();
            const descriptor = Object.getOwnPropertyDescriptor(C.prototype, key)!;
            const getter: any = descriptor.get, setter: any = descriptor.set;
            for (const fn of [getter, setter]) {
                const name = Object.getOwnPropertyDescriptor(fn, "name")!;
                console.log(name.value, name.writable, name.enumerable, name.configurable,
                    Object.hasOwn(fn, "prototype"));
            }
            Object.defineProperty(getter, "name", { value: "changed" });
            delete setter.name;
            new C<string>();
            const again = Object.getOwnPropertyDescriptor(C.prototype, key)!;
            console.log(again.get === getter, again.set === setter, getter.name, Object.hasOwn(setter, "name"));
            const receiver = { value: 42 };
            setter.call(receiver, 43);
            console.log(getter.call(receiver));
            """, "get [label] false false true false\nset [label] false false true false\ntrue true changed false\n43\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void RawSymbolAccessorNamesComeFromTheEvaluatedKey(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
