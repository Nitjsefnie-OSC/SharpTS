using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ForwardedReturnValueTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["any-alias", """
            function rawAny(): any { return null; }
            function anyAlias(): number { const alias: () => number = rawAny; return alias(); }
            function rawParameter(value: any): any { return value; }
            function parameterAlias(): boolean { const alias: (value: any) => boolean = rawParameter; return alias(undefined); }
            console.log(rawAny(), anyAlias(), parameterAlias());
            """, "null null undefined\n"];
        yield return ["bindings", """
            function safe(): number { return 7; }
            function replacement(): number { return undefined as any; }
            let mutable: () => number = safe;
            function forward(): number { return mutable(); }
            function shadow(safe: () => number): number { return safe(); }
            function block(): number { const safe = (): number => null as any; return safe(); }
            class C { read(): number { return null as any; } }
            function uncertain(value: C): number { return value.read(); }
            function exact(): number { const value = new C(); return value.read(); }
            function nativeFirst(): number { return nativeLast(); }
            function nativeLast(): number { return 8; }
            function cycleA(stop: boolean): number { return stop ? 9 : cycleB(true); }
            function cycleB(stop: boolean): number { return cycleA(stop); }
            console.log(forward());
            mutable = replacement;
            console.log(forward(), shadow(replacement), block(), uncertain(new C()), exact());
            console.log(nativeFirst(), cycleB(true));
            class ReplacingBase { constructor() { return { read: () => null as any } as any; } }
            class ReplacingDerived extends ReplacingBase { read(): number { return 7; } }
            function replaced(): number { const value = new ReplacingDerived(); return value.read(); }
            console.log(replaced());
            class Asserted { read(value: number = 0): number { return true as unknown as number; } }
            function asserted(): number { const value = new Asserted(); return value.read(); }
            console.log(new Asserted().read(), asserted());
            """, "7\nundefined undefined null null null\n8 9\nnull\ntrue true\n"];
        yield return ["branches", """
            function raw(): number { return null as any; }
            function rawFlag(): boolean { return undefined as any; }
            function branch(flag: boolean): number { return flag ? raw() : 7; }
            function logical(): boolean { return true && rawFlag(); }
            function numeric(): number { return +raw(); }
            function compare(): boolean { return raw() === 0; }
            function addition(): number { return raw() + 2; }
            console.log(branch(true), branch(false), logical());
            console.log(numeric(), compare(), addition());
            function nested(flag: boolean): number { return flag ? (true ? raw() : 1) : (false ? 2 : raw()); }
            console.log(nested(true), nested(false));
            """, "null 7 undefined\n0 false 2\nnull null\n"];
        yield return ["exact", """
            function raw(): number { return null as any; }
            function forward(): number { return raw(); }
            function rawFlag(): boolean { return undefined as any; }
            function forwardFlag(): boolean { return rawFlag(); }
            console.log(raw(), forward());
            console.log(rawFlag(), forwardFlag());
            """, "null null\nundefined undefined\n"];
        yield return ["passthrough", """
            function raw(): number { return null as any; }
            function rawFlag(): boolean { return undefined as any; }
            function grouped(): number { return ((raw() satisfies number) as number)!; }
            function comma(): number { return (console.log("comma"), raw()); }
            function nullish(): number { return raw() ?? 9; }
            function orValue(): number { return raw() || 4; }
            let storage: number = 0;
            function assignment(): number { return storage = raw(); }
            function nested(flag: boolean): number { return ((flag ? raw() : 7) as number)!; }
            console.log(grouped(), nullish(), orValue());
            console.log(comma());
            console.log(assignment(), storage, nested(true));
            """, "null 9 4\ncomma\nnull\nnull null null\n"];
        yield return ["wrappers", """
            function first(): number { return second(); }
            function second(): number { return raw(); }
            function raw(): number { return null as any; }
            function rawFlag(): boolean { return undefined as any; }
            function inferred() { return raw(); }
            const expression = (): number => raw();
            const block = (): boolean => { return rawFlag(); };
            const functionValue = function(): number { return raw(); };
            class C { read(): number { return raw(); } flag(): boolean { return rawFlag(); } }
            console.log(first(), second(), inferred(), expression(), block(), functionValue(), new C().read(), new C().flag());
            """, "null null null null undefined null null undefined\n"];
        yield return ["promise-callback", """
            function raw(): number { return null as any; }
            Promise.resolve(1).then((value: number): number => raw())
                .then((value: any) => console.log(value));
            """, "null\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ForwardedCallsRetainRawReturnValues(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}

