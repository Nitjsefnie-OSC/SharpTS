using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassConstructorIntegrityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["declaration-expression-string-descriptors", """
            function declared(): any {
                class Owner { static value = 3; static method() { return this; } }
                return Owner;
            }
            function expression(): any {
                return class Owner { static value = 3; static method() { return this; } };
            }
            for (const factory of [declared, expression]) {
                for (const kind of ['seal', 'freeze']) {
                    const owner: any = factory(), original = owner.method;
                    if (kind === 'seal') Object.seal(owner); else Object.freeze(owner);
                    const data: any = Object.getOwnPropertyDescriptor(owner, 'value');
                    const method: any = Object.getOwnPropertyDescriptor(owner, 'method');
                    console.log(kind, data.value, data.writable, data.enumerable, data.configurable,
                        method.writable, method.enumerable, method.configurable,
                        method.value === original, method.value.call(owner) === owner);
                    console.log(Reflect.deleteProperty(owner, 'value'), Reflect.deleteProperty(owner, 'method'),
                        Object.hasOwn(owner, 'value'), Object.hasOwn(owner, 'method'));
                }
            }
            """, "seal 3 true true false true false false true true\nfalse false true true\nfreeze 3 false true false false false false true true\nfalse false true true\nseal 3 true true false true false false true true\nfalse false true true\nfreeze 3 false true false false false false true true\nfalse false true true\n"];
        yield return ["declared-symbol-data-method-accessor", """
            for (const kind of ['seal', 'freeze']) {
                const dataKey = Symbol('data'), methodKey = Symbol('method'), accessorKey = Symbol('accessor');
                const state = { gets: 0, sets: 0, value: 1, correct: false };
                class Owner {
                    static [dataKey] = 5;
                    static [methodKey]() { return this; }
                    static get [accessorKey]() { state.gets++; return state.value; }
                    static set [accessorKey](value: number) { state.sets++; state.value = value; state.correct = this === Owner; }
                }
                const owner: any = Owner, original = owner[methodKey];
                const before: any = Object.getOwnPropertyDescriptor(owner, accessorKey);
                if (kind === 'seal') Object.seal(owner); else Object.freeze(owner);
                const data: any = Object.getOwnPropertyDescriptor(owner, dataKey);
                const method: any = Object.getOwnPropertyDescriptor(owner, methodKey);
                const accessor: any = Object.getOwnPropertyDescriptor(owner, accessorKey);
                console.log(kind, data.value, data.writable, data.enumerable, data.configurable,
                    method.writable, method.enumerable, method.configurable,
                    method.value === original, method.value.call(owner) === owner);
                console.log(accessor.get === before.get, accessor.set === before.set,
                    accessor.enumerable, accessor.configurable, Object.hasOwn(accessor, 'value'), state.gets);
                console.log(Reflect.deleteProperty(owner, dataKey), Reflect.deleteProperty(owner, methodKey),
                    Reflect.deleteProperty(owner, accessorKey), Object.hasOwn(owner, dataKey),
                    Object.hasOwn(owner, methodKey), Object.hasOwn(owner, accessorKey));
            }
            """, "seal 5 true true false true false false true true\ntrue true false false false 0\nfalse false false true true true\nfreeze 5 false true false false false false true true\ntrue true false false false 0\nfalse false false true true true\n"];
        yield return ["symbol-setter-remains-callable", """
            for (const kind of ['seal', 'freeze']) {
                const key = Symbol('accessor'), state = { calls: 0, value: 1, correct: false };
                class Owner {
                    static get [key]() { return state.value; }
                    static set [key](value: number) { state.calls++; state.value = value; state.correct = this === Owner; }
                }
                const owner: any = Owner;
                if (kind === 'seal') Object.seal(owner); else Object.freeze(owner);
                owner[key] = 9;
                console.log(kind, state.value, state.calls, state.correct, owner[key]);
            }
            """, "seal 9 1 true 9\nfreeze 9 1 true 9\n"];
        yield return ["sealed-writes-frozen-strict-and-setters", """
            function write(owner: any, key: string, value: any) { 'use strict'; owner[key] = value; }
            class Sealed { static value = 3; static method() { return 1; } }
            const sealed: any = Sealed, replacement = () => 2;
            Object.seal(sealed);
            write(sealed, 'value', 7);
            sealed.method = replacement;
            const sealedData: any = Object.getOwnPropertyDescriptor(sealed, 'value');
            const sealedMethod: any = Object.getOwnPropertyDescriptor(sealed, 'method');
            console.log(sealed.value, sealedData.writable, sealedData.enumerable, sealedData.configurable,
                sealed.method === replacement, sealedMethod.writable, sealedMethod.enumerable, sealedMethod.configurable);
            const state = { calls: 0, value: 1, correct: false };
            class Frozen {
                static value = 4;
                static get tracked() { return state.value; }
                static set tracked(value: number) { state.calls++; state.value = value; state.correct = this === Frozen; }
            }
            const frozen: any = Frozen, before: any = Object.getOwnPropertyDescriptor(frozen, 'tracked');
            Object.freeze(frozen);
            try { write(frozen, 'value', 8); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Reflect.set(frozen, 'value', 9), frozen.value,
                Object.getOwnPropertyDescriptor(frozen, 'value')!.writable);
            write(frozen, 'tracked', 11);
            const after: any = Object.getOwnPropertyDescriptor(frozen, 'tracked');
            console.log(state.calls, state.value, state.correct, after.get === before.get,
                after.set === before.set, after.configurable, Object.hasOwn(after, 'writable'));
            """, "7 true true false true true false false\ntrue\nfalse 4 false\n1 11 true true true false false\n"];
        yield return ["empty-classes-reject-late-definitions", """
            function declared(): any { class Owner {} return Owner; }
            function expression(): any { return class Owner {}; }
            for (const factory of [declared, expression]) {
                for (const kind of ['seal', 'freeze']) {
                    const owner: any = factory(), key = Symbol('new');
                    if (kind === 'seal') Object.seal(owner); else Object.freeze(owner);
                    console.log(kind, Object.isExtensible(owner),
                        Reflect.defineProperty(owner, 'added', { value: 1 }), Reflect.defineProperty(owner, key, { value: 2 }));
                    try { Object.defineProperty(owner, 'added', { value: 1 }); } catch (error) { console.log(error instanceof TypeError); }
                    try { Object.defineProperty(owner, key, { value: 2 }); } catch (error) { console.log(error instanceof TypeError); }
                    console.log(Object.hasOwn(owner, 'added'), Object.hasOwn(owner, key));
                }
            }
            """, "seal false false false\ntrue\ntrue\nfalse false\nfreeze false false false\ntrue\ntrue\nfalse false\nseal false false false\ntrue\ntrue\nfalse false\nfreeze false false false\ntrue\ntrue\nfalse false\n"];
        yield return ["repeated-integrity-compatible-definitions", """
            class Owner { static value = 3; static method() { return 1; } }
            const owner: any = Owner, replacement = () => 17;
            const state = { gets: 0, sets: 0, value: 1 };
            const getter = function() { state.gets++; return state.value; };
            const setter = function(value: number) { state.sets++; state.value = value; };
            Object.defineProperty(owner, 'method', {
                value: replacement, writable: true, enumerable: false, configurable: true
            });
            Object.defineProperty(owner, 'accessor', {
                get: getter, set: setter, enumerable: true, configurable: true
            });
            console.log('seal', Object.seal(owner) === owner, Object.seal(owner) === owner);
            console.log('sealed', Object.getOwnPropertyDescriptor(owner, 'value')!.writable,
                Object.getOwnPropertyDescriptor(owner, 'value')!.configurable,
                Object.getOwnPropertyDescriptor(owner, 'method')!.value === replacement,
                Object.getOwnPropertyDescriptor(owner, 'method')!.writable);
            console.log('update', Reflect.defineProperty(owner, 'value', { value: 7 }),
                Reflect.defineProperty(owner, 'value', { writable: false }),
                Reflect.defineProperty(owner, 'value', { writable: true }), owner.value);
            console.log('repeat', Object.freeze(owner) === owner, Object.freeze(owner) === owner,
                Object.seal(owner) === owner, Object.isFrozen(owner));
            const data: any = Object.getOwnPropertyDescriptor(owner, 'value');
            const method: any = Object.getOwnPropertyDescriptor(owner, 'method');
            console.log('flags', data.writable, data.enumerable, data.configurable,
                method.writable, method.enumerable, method.configurable);
            console.log('compatible', Reflect.defineProperty(owner, 'value', {}),
                Reflect.defineProperty(owner, 'value', { value: 7 }),
                Object.defineProperty(owner, 'value', { value: 7 }) === owner,
                Reflect.defineProperty(owner, 'accessor', { get: getter, set: setter }));
            console.log('rejected', Reflect.defineProperty(owner, 'value', { value: 8 }),
                Reflect.defineProperty(owner, 'value', { get: getter }),
                Reflect.defineProperty(owner, 'value', { configurable: true }),
                Reflect.defineProperty(owner, 'accessor', { set: undefined }));
            owner.accessor = 11;
            const accessor: any = Object.getOwnPropertyDescriptor(owner, 'accessor');
            console.log('stable', owner.value, owner.method === replacement, owner.method(),
                accessor.get === getter, accessor.set === setter, accessor.enumerable, accessor.configurable,
                Object.hasOwn(accessor, 'value'), state.gets, state.sets, state.value);
            """, "seal true true\nsealed true false true true\nupdate true true false 7\nrepeat true true true true\nflags false true false false false false\ncompatible true true true true\nrejected false false false false\nstable 7 true 17 true true true false false 0 1 11\n"];
        yield return ["child-freeze-preserves-parent-and-instance-prototype", """
            const dataKey = Symbol('data'), methodKey = Symbol('method');
            const state = { value: 0, calls: 0, correct: false };
            class Parent {
                static inherited = 3;
                static [dataKey] = 5;
                static [methodKey]() { return this; }
                static set inheritedSetter(value: number) { state.value = value; state.calls++; state.correct = this === Child; }
            }
            class Child extends Parent { static own = 7; }
            const child: any = Child, parent: any = Parent, prototype: any = Child.prototype;
            Object.freeze(child);
            const own: any = Object.getOwnPropertyDescriptor(child, 'own');
            const inherited: any = Object.getOwnPropertyDescriptor(parent, 'inherited');
            const data: any = Object.getOwnPropertyDescriptor(parent, dataKey);
            const method: any = Object.getOwnPropertyDescriptor(parent, methodKey);
            console.log(own.writable, own.configurable, inherited.writable, inherited.configurable,
                data.writable, data.configurable, method.writable, method.configurable);
            console.log(Object.getOwnPropertyDescriptor(child, 'inherited') === undefined,
                Object.getOwnPropertyDescriptor(child, dataKey) === undefined,
                Object.getOwnPropertyDescriptor(child, methodKey) === undefined,
                Object.hasOwn(child, 'inherited'), Object.hasOwn(child, dataKey), Object.hasOwn(child, methodKey));
            console.log(delete child.inherited, delete child[dataKey], delete child[methodKey], parent.inherited, parent[dataKey]);
            child.inheritedSetter = 9;
            console.log(state.value, state.calls, state.correct, Object.hasOwn(child, 'inheritedSetter'));
            parent.inherited = 11;
            prototype.extra = 13;
            const instance: any = new Child();
            console.log(parent.inherited, child.inherited, instance instanceof Child, prototype.extra,
                Object.isExtensible(parent), Object.isFrozen(prototype));
            """, "false false true true true true true true\ntrue true true false false false\ntrue true true 3 5\n9 1 true false\n11 11 true 13 true false\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] switch
            {
                // #2630: compiled frozen constructors suppress an own Symbol setter.
                "symbol-setter-remains-callable" => false,
                // #2575: compiled compatible definitions of frozen string slots are rejected.
                "repeated-integrity-compatible-definitions" => false,
                _ => true
            })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ClassIntegrityAppliesToOwnDescriptors(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
