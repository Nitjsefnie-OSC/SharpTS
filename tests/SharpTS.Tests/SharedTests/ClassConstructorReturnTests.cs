using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassConstructorReturnTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["direct", """
            const replacement: any = { value: 43, read() { return this.value; } };
            class C {
                value = 1;
                read() { return this.value; }
                constructor() { return replacement; }
            }
            const value = new C();
            console.log(value === replacement, value.value, value.read(), new C().value);
            const bind: any = (() => 0).bind;
            const Bound: any = bind.call(C, {});
            const BoundAgain: any = Bound.bind({});
            console.log(new Bound() === replacement, new BoundAgain() === replacement);
            console.log(value instanceof C);
            function identity(value: C): C { value.value = 44; return value; }
            const forwarded = identity(value);
            console.log(forwarded === replacement, forwarded.read(), new C().read());
            """, "true 43 43 43\ntrue true\nfalse\ntrue 44 44\n"];
        yield return ["expression", """
            const replacement: any = { value: 43 };
            const C = class { constructor(value: number) { return replacement; } };
            const Bound: any = C.bind({}, 1);
            const value = new C(1);
            console.log(value === replacement, (value as any).value, new Bound() === replacement);
            """, "true 43 true\n"];
        yield return ["objects", """
            class Other { value = 43; }
            class C { constructor(value: any) { return value; } }
            const values: any[] = [{ value: 43 }, [43], () => 43, new Other(), new Date(0), new Map()];
            for (const value of values) {
                const Bound: any = C.bind({}, value);
                console.log(new C(value) === value, new Bound() === value);
            }
            const callable: any = new C(() => 43);
            console.log(typeof callable, callable());
            """, "true true\ntrue true\ntrue true\ntrue true\ntrue true\ntrue true\nfunction 43\n"];
        yield return ["primitives", """
            class C { value = 43; constructor(value: any) { return value; } }
            const values: any[] = [1, "text", true, null, undefined, Symbol("s"), 1n];
            for (const value of values) {
                const Bound: any = C.bind({}, value);
                const direct = new C(value), bound = new Bound();
                console.log(direct.value, bound.value, direct instanceof C, direct !== bound);
            }
            class Empty { value = 43; constructor() {} }
            class Bare { value = 43; constructor() { return; } }
            console.log(new Empty().value, new Bare().value);
            class PrimitiveBase { value = 44; constructor() { return (1 as any); } }
            class PrimitiveDerived extends PrimitiveBase {}
            console.log(new PrimitiveDerived().value);
            """, "43 43 true true\n43 43 true true\n43 43 true true\n43 43 true true\n43 43 true true\n43 43 true true\n43 43 true true\n43 43\n44\n"];
        yield return ["derived", """
            class Base { value = 43; }
            class C extends Base {
                constructor(value: any) { super(); return value; }
            }
            const replacement: any = { value: 44 };
            console.log(new C(replacement) === replacement, new C(undefined).value);
            const values: any[] = [1, "text", true, null, Symbol("s"), 1n];
            for (const value of values) {
                try { new C(value); } catch (error) { console.log(error instanceof TypeError); }
                const Bound: any = C.bind({}, value);
                try { new Bound(); } catch (error) { console.log(error instanceof TypeError); }
            }
            """, "true 43\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\n"];
        yield return ["finally", """
            const first: any = { value: 1 }, replacement: any = { value: 43 };
            class C {
                constructor() { try { return first; } finally { return replacement; } }
            }
            class Base {}
            class Derived extends Base {
                constructor() { super(); try { return (1 as any); } finally { return replacement; } }
            }
            class Cleared {
                value = 44;
                constructor() { try { return replacement; } finally { return; } }
            }
            console.log(new C() === replacement, new Derived() === replacement, new Cleared().value);
            class Cancelled {
                value = 45;
                constructor() { done: { try { return replacement; } finally { break done; } } }
            }
            console.log(new Cancelled().value);
            """, "true true 44\n45\n"];
        yield return ["inherited", """
            const replacement: any = { value: 43 };
            class Base { constructor() { return replacement; } }
            class Derived extends Base {}
            class Middle extends Derived {}
            const Bound: any = Middle.bind({});
            console.log(new Derived() === replacement, new Middle() === replacement, new Bound() === replacement);
            """, "true true true\n"];
        yield return ["suspended", """
            const replacement: any = { value: 43 };
            class C { constructor(value: any) { return value; } }
            function* values() { yield new C(replacement); }
            console.log(values().next().value === replacement);
            async function run() {
                const result = new C(await Promise.resolve(replacement));
                console.log(result === replacement, (result as any).value);
            }
            run();
            """, "true\ntrue 43\n"];
        yield return ["generic", """
            const replacement: any = { value: 43 };
            class C<T> { value = 1; constructor(value: T) { return replacement; } }
            const E = class<T> { value = 1; constructor(value: T) { return replacement; } };
            console.log(new C<number>(1) === replacement, new C(1).value);
            console.log(new E<number>(1) === replacement, new E(1).value);
            """, "true 43\ntrue 43\n"];
        yield return ["intrinsic", """
            const replacement: any = { value: 43 };
            class A extends Array<any> { constructor(value: any) { super(); return value; } }
            class E extends Error { constructor(value: any) { super("message"); return value; } }
            class P extends Promise<any> { constructor(value: any) { super(resolve => resolve(1)); return value; } }
            const constructors: any[] = [A, E, P];
            for (const C of constructors) {
                const Bound: any = C.bind({}, replacement);
                console.log(new C(replacement) === replacement, new Bound() === replacement);
                console.log(new C(undefined) instanceof C);
                try { new C(null); } catch (error) { console.log(error instanceof TypeError); }
            }
            """, "true true\ntrue\ntrue\ntrue true\ntrue\ntrue\ntrue true\ntrue\ntrue\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[1], row[2], mode }));

    [Theory]
    [MemberData(nameof(ModeCases))]
    public void ConstructorReturnPreservesGuestConstruction(string source, string expected, ExecutionMode mode)
    {
        if (mode == ExecutionMode.Compiled)
            Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
