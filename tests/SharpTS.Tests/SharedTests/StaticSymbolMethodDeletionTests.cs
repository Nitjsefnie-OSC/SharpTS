using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticSymbolMethodDeletionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-and-retained-identity", """
            const key = Symbol('method'), other = Symbol('method');
            class Direct { static [key]() { return 7; } static [other]() { return 9; } }
            class Reflected { static [key]() { return 7; } }
            const direct: any = Direct, reflected: any = Reflected, original = direct[key];
            console.log(delete direct[key], direct[key] === undefined,
                Object.getOwnPropertyDescriptor(direct, key) === undefined);
            console.log(Reflect.deleteProperty(reflected, key), reflected[key] === undefined,
                Object.getOwnPropertyDescriptor(reflected, key) === undefined);
            console.log(original(), direct[other](), Object.hasOwn(direct, other),
                !Object.getOwnPropertySymbols(direct).includes(key), Object.getOwnPropertySymbols(direct)[0] === other);
            """, "true true true\ntrue true true\n7 9 true true true\n"];
        yield return ["redefine-reassign-and-repeated-delete", """
            const key = Symbol('method');
            class Declared { static [key]() { return 1; } }
            const expression = class { static [key]() { return 1; } };
            for (const owner of [Declared as any, expression as any]) {
                console.log(delete owner[key], delete owner[key], owner[key] === undefined);
                Object.defineProperty(owner, key, { value: () => 2, writable: true, enumerable: true, configurable: true });
                console.log(owner[key](), Reflect.deleteProperty(owner, key), owner[key] === undefined);
                owner[key] = () => 3;
                const descriptor: any = Object.getOwnPropertyDescriptor(owner, key);
                console.log(descriptor.value === owner[key], descriptor.writable, descriptor.enumerable,
                    descriptor.configurable, owner[key](), Reflect.ownKeys(owner).includes(key));
                console.log(Reflect.deleteProperty(owner, key), !Object.hasOwn(owner, key),
                    !Object.getOwnPropertySymbols(owner).includes(key));
            }
            """, string.Concat(Enumerable.Repeat("true true true\n2 true true\ntrue true true true 3 true\ntrue true true\n", 2))];
        yield return ["deleting-child-override-reveals-live-parent", """
            const key = Symbol('method');
            class Parent { static [key]() { return 1; } }
            class Child extends Parent { static [key]() { return 2; } }
            const parent: any = Parent, child: any = Child, retained = child[key];
            console.log(delete child[key], child[key] === parent[key], child[key](), retained(),
                !Object.hasOwn(child, key), !Reflect.ownKeys(child).includes(key));
            console.log(delete child[key], parent[key](), Object.hasOwn(parent, key));
            parent[key] = () => 3;
            console.log(child[key](), child[key] === parent[key]);
            console.log(Reflect.deleteProperty(parent, key), child[key] === undefined,
                Object.getOwnPropertyDescriptor(parent, key) === undefined);
            """, "true true 1 2 true true\ntrue 1 true\n3 true\ntrue true true\n"];
        yield return ["integrity-does-not-resurrect-deleted-methods", """
            function strictDelete(owner: any, key: symbol) { 'use strict'; return delete owner[key]; }
            for (const kind of ['seal', 'freeze']) {
                const key = Symbol('method');
                class Gone { static [key]() { return 7; } }
                class Kept { static [key]() { return 9; } }
                const gone: any = Gone, kept: any = Kept, retained = kept[key];
                Reflect.deleteProperty(gone, key);
                if (kind === 'seal') { Object.seal(gone); Object.seal(kept); }
                else { Object.freeze(gone); Object.freeze(kept); }
                console.log(kind, gone[key] === undefined, !Object.hasOwn(gone, key),
                    !Reflect.ownKeys(gone).includes(key), Reflect.defineProperty(gone, key, { value: 1 }));
                console.log(Reflect.deleteProperty(kept, key), kept[key] === retained,
                    Object.getOwnPropertyDescriptor(kept, key)!.configurable, kept[key]());
                try { strictDelete(kept, key); } catch (error) { console.log(error instanceof TypeError); }
            }
            """, "seal true true true false\nfalse true false 9\ntrue\nfreeze true true true false\nfalse true false 9\ntrue\n"];
        yield return ["nonextensible-deletion-and-recreation", """
            const key = Symbol('method');
            class Owner { static [key]() { return 7; } }
            const owner: any = Owner, retained = owner[key];
            Object.preventExtensions(owner);
            console.log(Reflect.deleteProperty(owner, key), owner[key] === undefined, retained());
            owner[key] = () => 9;
            console.log(!Object.hasOwn(owner, key), !Reflect.ownKeys(owner).includes(key));
            console.log(Reflect.defineProperty(owner, key, { value: () => 11 }), !Object.hasOwn(owner, key));
            """, "true true 7\ntrue true\nfalse true\n"];
        yield return ["explicit-definitions-respect-extensibility", """
            const key = Symbol('new');
            class Owner { static value = 3; }
            const owner: any = Owner;
            Object.preventExtensions(owner);
            console.log(Reflect.defineProperty(owner, 'added', { value: 1 }),
                Reflect.defineProperty(owner, key, { value: 2 }), !Object.hasOwn(owner, 'added'), !Object.hasOwn(owner, key));
            try { Object.defineProperty(owner, 'added', { value: 1 }); } catch (error) { console.log(error instanceof TypeError); }
            try { Object.defineProperty(owner, key, { value: 2 }); } catch (error) { console.log(error instanceof TypeError); }
            """, "false false true true\ntrue\ntrue\n"];
        yield return ["existing-field-definition-on-nonextensible-class", """
            class Owner { static value = 3; }
            const owner: any = Owner;
            Object.preventExtensions(owner);
            const before: any = Object.getOwnPropertyDescriptor(owner, 'value');
            console.log(Object.hasOwn(owner, 'value'), before.writable, before.configurable);
            console.log(Reflect.defineProperty(owner, 'value', { value: 4 }), owner.value);
            """, "true true true\ntrue 4\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // #2642: compiled nonextensible constructors reject existing static-field updates.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] != "existing-field-definition-on-nonextensible-class")
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DeletionRemovesOnlyTheLiveOwnSymbolMethod(string name, string source, string expected, ExecutionMode mode)
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
