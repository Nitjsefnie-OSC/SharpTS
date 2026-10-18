using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticMethodPartialDescriptorTests
{
    private const string IntegritySource = """
        const symbol = Symbol('method'), kind = '__MODE__';
        for (const useSymbol of [false, true]) {
            const key: any = useSymbol ? symbol : 'method';
            const owner: any = useSymbol ? class { static [symbol]() { return 7; } }
                : class { static method() { return 7; } };
            const original = owner[key], replacement = () => 9;
            if (kind === 'nonextensible') Object.preventExtensions(owner);
            else if (kind === 'sealed') Object.seal(owner); else Object.freeze(owner);
            const empty = Reflect.defineProperty(owner, key, {});
            const same = Reflect.defineProperty(owner, key, { value: original });
            const readonly = Reflect.defineProperty(owner, key, { writable: false });
            const after: any = Object.getOwnPropertyDescriptor(owner, key);
            console.log(kind, typeof key, empty, same, readonly, after.value === original,
                after.writable, after.enumerable, after.configurable);
            console.log('replace', Reflect.defineProperty(owner, key, { value: replacement }),
                owner[key] === (kind === 'nonextensible' ? replacement : original));
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["attribute-only-method-updates", """
            const symbol = Symbol('method');
            for (const useSymbol of [false, true]) {
                for (const reflected of [false, true]) {
                    const key: any = useSymbol ? symbol : 'method';
                    const owner: any = useSymbol ? class { static [symbol]() { return 7; } }
                        : class { static method() { return 7; } };
                    const original = owner[key], before: any = Object.getOwnPropertyDescriptor(owner, key);
                    const result = reflected ? Reflect.defineProperty(owner, key, { writable: false })
                        : Object.defineProperty(owner, key, { writable: false }) === owner;
                    const after: any = Object.getOwnPropertyDescriptor(owner, key);
                    console.log(reflected, typeof key, result, before.value === after.value, after.value === original,
                        after.writable, after.enumerable, after.configurable, typeof owner[key], owner[key]());
                    const changed = Reflect.defineProperty(owner, key, { enumerable: true });
                    const enumerable: any = Object.getOwnPropertyDescriptor(owner, key);
                    console.log(changed, enumerable.value === original, enumerable.writable,
                        enumerable.enumerable, enumerable.configurable);
                }
            }
            """, "false string true true true false false true function 7\ntrue true false true true\n"
                + "true string true true true false false true function 7\ntrue true false true true\n"
                + "false symbol true true true false false true function 7\ntrue true false true true\n"
                + "true symbol true true true false false true function 7\ntrue true false true true\n"];
        yield return ["omitted-and-explicit-values", """
            const symbol = Symbol('method');
            for (const useSymbol of [false, true]) {
                const key: any = useSymbol ? symbol : 'method';
                const owner: any = useSymbol ? class { static [symbol]() { return 7; } }
                    : class { static method() { return 7; } };
                const original = owner[key], replacement = () => 9;
                Reflect.defineProperty(owner, key, {});
                const empty: any = Object.getOwnPropertyDescriptor(owner, key);
                console.log(empty.value === original, empty.writable, empty.enumerable, empty.configurable);
                Object.defineProperty(owner, key, { value: undefined });
                const explicit: any = Object.getOwnPropertyDescriptor(owner, key);
                console.log(owner[key] === undefined, Object.hasOwn(explicit, 'value'), explicit.writable,
                    explicit.enumerable, explicit.configurable);
                Reflect.defineProperty(owner, key, { value: replacement });
                const replaced: any = Object.getOwnPropertyDescriptor(owner, key);
                console.log(owner[key] === replacement, owner[key](), replaced.writable, replaced.enumerable, replaced.configurable);
            }
            class Field {}
            const field: any = Field, data = Symbol('data');
            field[data] = 11;
            Object.defineProperty(field, data, { writable: false });
            const descriptor: any = Object.getOwnPropertyDescriptor(field, data);
            console.log(descriptor.value, field[data], descriptor.writable, descriptor.enumerable, descriptor.configurable);
            """, string.Concat(Enumerable.Repeat("true true false true\ntrue true true false true\ntrue 9 true false true\n", 2))
                + "11 11 false true true\n"];
        yield return ["method-accessor-generic-descriptor", """
            const symbol = Symbol('method');
            for (const useSymbol of [false, true]) {
                const key: any = useSymbol ? symbol : 'method', state = { gets: 0 };
                const owner: any = useSymbol ? class { static [symbol]() { return 7; } }
                    : class { static method() { return 7; } };
                const getter = function(this: any) { state.gets++; return this === owner ? 9 : -1; };
                Object.defineProperty(owner, key, { get: getter });
                const before: any = Object.getOwnPropertyDescriptor(owner, key);
                Reflect.defineProperty(owner, key, { enumerable: true });
                const after: any = Object.getOwnPropertyDescriptor(owner, key);
                console.log(before.get === getter, after.get === getter, !Object.hasOwn(after, 'value'),
                    !Object.hasOwn(after, 'writable'), after.enumerable, after.configurable, state.gets);
                console.log(owner[key], state.gets);
                console.log(Reflect.defineProperty(owner, key, { get: undefined }), owner[key] === undefined, state.gets);
            }
            """, string.Concat(Enumerable.Repeat("true true true true true true 0\n9 1\ntrue true 1\n", 2))];
        yield return ["own-only-and-deleted-method-defaults", """
            const symbol = Symbol('method');
            for (const key of ['method', symbol]) {
                class Parent { static method() { return 7; } static [symbol]() { return 8; } }
                class Child extends Parent {}
                class Deleted { static method() { return 1; } static [symbol]() { return 2; } }
                const parent: any = Parent, child: any = Child, deleted: any = Deleted;
                const inherited = parent[key], retained = deleted[key];
                Object.defineProperty(child, key, { writable: false });
                const shadow: any = Object.getOwnPropertyDescriptor(child, key);
                console.log(child[key] === undefined, shadow.value === undefined, shadow.writable,
                    shadow.enumerable, shadow.configurable, parent[key] === inherited, inherited());
                Reflect.deleteProperty(deleted, key);
                Reflect.defineProperty(deleted, key, { writable: false });
                const fresh: any = Object.getOwnPropertyDescriptor(deleted, key);
                console.log(deleted[key] === undefined, fresh.value === undefined, fresh.writable,
                    fresh.enumerable, fresh.configurable, retained());
            }
            """, "true true false false false true 7\ntrue true false false false 1\n"
                + "true true false false false true 8\ntrue true false false false 2\n"];
        foreach (var kind in new[] { "nonextensible", "sealed", "frozen" })
        {
            string configurable = kind == "nonextensible" ? "true" : "false";
            yield return ["integrity-" + kind, IntegritySource.Replace("__MODE__", kind),
                string.Concat(new[] { "string", "symbol" }.Select(keyType =>
                    $"{kind} {keyType} true true true true false false {configurable}\nreplace {configurable} true\n"))];
        }
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] switch
            {
                // #2643: compiled sealed readonly properties accept a changed value.
                "integrity-sealed" => false,
                // #2575 rejects frozen string no-ops; #2372 permits changing frozen Symbols.
                "integrity-frozen" => false,
                _ => true
            })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void PartialUpdatesPreserveExistingOwnStaticDescriptors(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
