using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NamedClassConstructorPrivateFieldTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact", """
            const C = class Inner {
                static #m = 42;
                static read(value: typeof Inner) { return value.#m; }
            };
            console.log(C.read(C));
            """, "42\n"];
        yield return ["named-set", """
            const C = class Inner {
                static #m = 42;
                static write(value: typeof Inner) { value.#m = 43; return value.#m; }
            };
            console.log(C.write(C));
            """, "43\n"];
        yield return ["named-no-access", """
            const C = class Inner {
                static #m = 42;
                static read(value: typeof Inner) { return 42; }
            };
            console.log(C.read(C));
            """, "42\n"];
        yield return ["generic-access", """
            const C = class Inner<T> {
                static #m = 42;
                static read(value: typeof Inner) { return value.#m; }
            };
            console.log(C.read(C));
            """, "42\n"];
        yield return ["generic-no-access", """
            const C = class Inner<T> {
                static #m = 42;
                static read(value: typeof Inner) { return 42; }
            };
            console.log(C.read(C));
            """, "42\n"];
        yield return ["constructor-identity", """
            const C = class Inner {
                static same(value: typeof Inner) { return value === Inner; }
            };
            const alias = C;
            console.log(C.same(C), C.same(alias));
            """, "true true\n"];
        yield return ["constructor-alias", """
            const C = class Inner {
                static #m = 42;
                static read(value: typeof Inner) { return value.#m; }
            };
            const alias = C;
            console.log(C.read(alias));
            """, "42\n"];
        yield return ["ordinary-constructor", """
            class C {}
            function read(value: typeof C) { return value; }
            console.log(read(C) === C);
            """, "true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void NamedConstructorTypesRetainTheirDeclarationIdentity(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    public static IEnumerable<object[]> RejectedCases()
    {
        yield return ["""
            const C = class Inner { static #m = 42; static read(value: typeof Inner) { return value.#m; } }; C.read(new C());
            """, "TS2345"];
        yield return ["""
            const C = class Inner<T> { static #m = 42; static read(value: typeof Inner) { return value.#m; } }; C.read(new C<number>());
            """, "TS2345"];
        yield return ["""
            const First = class Same {
                #instance = 1;
                static #m = 42;
                static read(value: typeof Same) { return value.#m; }
            };
            const Other = class Same {
                #instance = 2;
                static #m = 43;
                static read(value: typeof Same) { return value.#m; }
            };
            console.log(First.read(Other));
            """, "TS2345"];
        yield return ["""
            const C = class Inner { static #m: number = 42; static write(value: typeof Inner) { value.#m = "bad"; } };
            """, "TS2322"];
    }

    [Theory, MemberData(nameof(RejectedCases))]
    public void ConstructorIdentityStillChecksCategoryOwnerAndFieldType(string source, string code)
    {
        var error = Assert.Throws<TypeCheckException>(() => TestHarness.RunInterpreted(source));
        Assert.Equal(code, error.Diagnostic.TsCode);
    }
}
