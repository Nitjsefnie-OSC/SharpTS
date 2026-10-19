using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArraySubclassPrototypeMutationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-null-membership", """
            class ArrayParent extends Array {}
            class ArrayChild extends ArrayParent {}
            const array: any = new ArrayChild();
            Object.setPrototypeOf(array, null);
            console.log(array instanceof ArrayChild, array instanceof ArrayParent,
                array instanceof Array, array instanceof Object);
            """, "false false false false\n"];
        yield return ["default-membership-chain", """
            class Parent extends Array {}
            class Child extends Parent {}
            const values: any = new Child();
            console.log(Object.getPrototypeOf(values) === Child.prototype,
                Object.getPrototypeOf(Child.prototype) === Parent.prototype,
                Object.getPrototypeOf(Parent.prototype) === Array.prototype);
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object, Array.isArray(values));
            const structural: any = Object.create(Child.prototype);
            console.log(structural instanceof Child, structural instanceof Parent,
                structural instanceof Array, Array.isArray(structural));
            """, "true true true\ntrue true true true true\ntrue true true false\n"];
        yield return ["constructor-prototype-identity", """
            class Parent extends Array {}
            class Child extends Parent {}
            const values: any = new Child();
            console.log(Object.getPrototypeOf(Parent) === Array, Object.getPrototypeOf(Child) === Parent,
                Object.getPrototypeOf(Parent).prototype === Array.prototype,
                values instanceof Object.getPrototypeOf(Parent));
            """, "true true true true\n"];
        yield return ["live-prototype-membership", """
            class Parent extends Array {}
            class Child extends Parent {}
            const values: any = new Child();
            const original = Object.getPrototypeOf(Child.prototype);
            Object.setPrototypeOf(Child.prototype, null);
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object, Array.isArray(values));
            Object.setPrototypeOf(Child.prototype, original);
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object);
            Object.setPrototypeOf(values, {});
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object, Array.isArray(values));
            Object.setPrototypeOf(values, Child.prototype);
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object);
            """, "true false false false true\ntrue true true true\nfalse false false true true\ntrue true true true\n"];
        yield return ["intrinsic-and-structural-membership", """
            class Values extends Array {}
            const hasInstance: any = (Function.prototype as any)[Symbol.hasInstance];
            const values: any = new Values();
            console.log(hasInstance.call(Values, values), hasInstance.call(Array, values));
            Object.setPrototypeOf(values, null);
            console.log(hasInstance.call(Values, values), hasInstance.call(Array, values));
            Object.setPrototypeOf(values, Array.prototype);
            console.log(hasInstance.call(Values, values), hasInstance.call(Array, values));
            console.log(Array.prototype instanceof Array, Array.isArray(Array.prototype));
            const structural: any = Object.create(Array.prototype);
            console.log(structural instanceof Array, Array.isArray(structural));
            const plain: any = [];
            Object.setPrototypeOf(plain, null);
            console.log(plain instanceof Array, plain instanceof Object, Array.isArray(plain));
            Object.setPrototypeOf(plain, Array.prototype);
            console.log(plain instanceof Array, plain instanceof Object);
            """, "true true\nfalse false\nfalse true\nfalse true\ntrue false\nfalse false true\ntrue true\n"];
        yield return ["arguments-membership", """
            function inspect() {
                console.log(Object.getPrototypeOf(arguments) === Object.prototype,
                    arguments instanceof Array, arguments instanceof Object, Array.isArray(arguments));
                Object.setPrototypeOf(arguments, Array.prototype);
                console.log(arguments instanceof Array, arguments instanceof Object, Array.isArray(arguments));
                Object.setPrototypeOf(arguments, null);
                console.log(arguments instanceof Array, arguments instanceof Object, Array.isArray(arguments));
            }
            inspect();
            """, "true false true false\ntrue true false\nfalse false false\n"];
        yield return ["arguments-own-iterator", """
            const original = Array.prototype.values;
            delete (Array.prototype as any).values;
            function inspect(first: any, second: any) {
                const values: any = arguments;
                const descriptor: any = Object.getOwnPropertyDescriptor(values, Symbol.iterator);
                console.log(Object.hasOwn(values, Symbol.iterator), descriptor.value === original,
                    descriptor.writable, descriptor.enumerable, descriptor.configurable);
                Object.setPrototypeOf(values, null);
                const iterator = values[Symbol.iterator]();
                console.log(iterator.next().value, iterator.next().value, iterator.next().done);
            }
            try { inspect(3, 4); }
            finally { (Array.prototype as any).values = original; }
            """, "true true true false true\n3 4 true\n"];
        yield return ["array-own-symbol-presence", """
            const values: any = [], data = Symbol(), accessor = Symbol();
            values[data] = 7;
            Object.defineProperty(values, accessor, { set(value: any) {}, configurable: true });
            console.log(Object.hasOwn(values, data), Object.hasOwn(values, accessor),
                Object.prototype.hasOwnProperty.call(values, data),
                Object.prototype.hasOwnProperty.call(values, accessor));
            console.log(delete values[data], delete values[accessor],
                Object.hasOwn(values, data), Object.hasOwn(values, accessor));
            """, "true true true true\ntrue true false false\n"];
        yield return ["null-preserves-exotic-storage", """
            class Values extends Array { tag = 'own'; read() { return this.tag; } }
            const values: any = new Values(3, 4);
            Object.setPrototypeOf(values, null);
            console.log(Array.isArray(values), values.length, values[0], values.tag);
            console.log(typeof values.push, typeof values.read);
            values[1] = 9;
            values.length = 1;
            console.log(values.length, values[0], 1 in values);
            console.log(Array.prototype.push.call(values, 7), values.length, values[1]);
            """, "true 2 3 own\nundefined undefined\n1 3 false\n2 2 7\n"];
        yield return ["custom-prototype-properties", """
            class Values extends Array { old() { return 'old'; } }
            const values: any = new Values(7, 8);
            const custom: any = {
                marker: 'custom',
                get first() { return (this as any)[0]; },
                read() { return (this as any)[1]; }
            };
            console.log(Object.setPrototypeOf(values, custom) === values);
            console.log(Object.getPrototypeOf(values) === custom, values.marker, values.first, values.read());
            console.log(typeof values.push, typeof values.old, Array.isArray(values));
            """, "true\ntrue custom 7 8\nundefined undefined true\n"];
        yield return ["reflect-and-restored-membership", """
            class Parent extends Array {}
            class Child extends Parent {}
            const values: any = new Child(1, 2);
            const original = Object.getPrototypeOf(values);
            console.log(Reflect.setPrototypeOf(values, null), Object.getPrototypeOf(values) === null);
            console.log(Reflect.setPrototypeOf(values, original), Object.getPrototypeOf(values) === original);
            console.log(values instanceof Child, values instanceof Parent, values instanceof Array, values instanceof Object);
            """, "true true\ntrue true\ntrue true true true\n"];
        yield return ["restored-identity-reads", """
            class Values extends Array {
                tag = "own";
                read() { return (this as any)[0]; }
            }
            const values: any = new Values(3, 4);
            const original: any = Object.getPrototypeOf(values);
            Object.setPrototypeOf(values, null);
            console.log(original === Values.prototype, typeof values.read, values.tag, values[0]);
            Object.setPrototypeOf(values, original);
            console.log(Object.getPrototypeOf(values) === original,
                values.read === Values.prototype.read, values.read(), values.push(5), values[2]);
            """, "true undefined own 3\ntrue true 3 3 5\n"];
        yield return ["live-prototype-methods", """
            class Parent extends Array {
                read() { return "P" + (this as any)[0]; }
            }
            class Values extends Parent {
                read() { return "C" + (this as any)[0]; }
            }
            const values: any = new Values(7, 8);
            console.log(values.read === Values.prototype.read, values.read());
            console.log(delete (Values.prototype as any).read);
            console.log(values.read === Parent.prototype.read, values.read());
            Object.defineProperty(Values.prototype, "read", {
                value: function(this: any) { return "R" + this[1]; }, configurable: true
            });
            console.log(values.read === Reflect.get(Values.prototype, "read"), values.read());
            const custom: any = { read() { return "X" + (this as any)[0]; } };
            Object.setPrototypeOf(Values.prototype, custom);
            delete (Values.prototype as any).read;
            console.log(values.read === custom.read, values.read(), typeof values.push);
            """, "true C7\ntrue\ntrue P7\ntrue R8\ntrue X7 undefined\n"];
        yield return ["inherited-numeric-getter", """
            class Values extends Array {
                "01" = "leading";
                "+0" = "plus";
                "-0" = "minus";
                "4294967295" = "large";
            }
            const values: any = new Values(10, 20), state = { gets: 0 };
            delete values[1];
            Object.defineProperty(Values.prototype, "1", {
                get: function(this: any) { state.gets++; return this[0] + 1; }, configurable: true
            });
            console.log(values[1], Reflect.get(values, "1"), state.gets, 1 in values, values.length);
            Object.defineProperty(values, "1", { value: 99, writable: true, configurable: true });
            console.log(values[1], state.gets, Object.hasOwn(values, "1"));
            delete values[1];
            console.log(values[1], state.gets);
            console.log(values[0], values["01"], values["+0"], values["-0"], values["4294967295"]);
            """, "11 11 2 true 2\n99 2 true\n11 3\n10 leading plus minus large\n"];
        yield return ["custom-accessor-receivers", """
            class Values extends Array {}
            const values: any = new Values(3, 4), key = Symbol("read");
            const custom: any = Object.create(Values.prototype), state = { strings: 0, symbols: 0 };
            Object.defineProperty(custom, "value", {
                get: function(this: any) { state.strings++; return this[0]; }, configurable: true
            });
            Object.defineProperty(custom, key, {
                get: function(this: any) { state.symbols++; return this[1]; }, configurable: true
            });
            Object.setPrototypeOf(values, custom);
            const receiver: any = { 0: 30, 1: 40 };
            console.log(values.value, values[key], Reflect.get(values, "value", receiver),
                Reflect.get(values, key, receiver), state.strings, state.symbols);
            console.log("value" in values, key in values, state.strings, state.symbols);
            """, "3 4 30 40 2 2\ntrue true 2 2\n"];
        yield return ["getterless-presence", """
            class Values extends Array { read() { return 'class'; } }
            const values: any = new Values(1, 2);
            const ownSymbol = Symbol('own'), prototypeSymbol = Symbol('prototype');
            let reads = 0, writes = 0;
            const tail: any = Object.create(Values.prototype);
            for (const key of ['ownMask', 'prototypeMask', ownSymbol, prototypeSymbol]) {
                Object.defineProperty(tail, key, {
                    get() { reads++; return 'deeper'; }, enumerable: false, configurable: true
                });
            }
            Object.defineProperty(tail, 'visible', {
                get() { reads++; return 7; }, enumerable: false, configurable: true
            });
            const prototype: any = Object.create(tail);
            for (const key of ['prototypeMask', prototypeSymbol]) {
                Object.defineProperty(prototype, key, {
                    set(value: any) { writes++; }, enumerable: false, configurable: true
                });
            }
            Object.setPrototypeOf(values, prototype);
            for (const key of ['ownMask', ownSymbol]) {
                Object.defineProperty(values, key, {
                    set(value: any) { writes++; }, enumerable: false, configurable: true
                });
            }
            console.log('presence', 'ownMask' in values, 'prototypeMask' in values,
                ownSymbol in values, prototypeSymbol in values, 'visible' in values,
                'read' in values, 'missing' in values, reads, writes);
            console.log('masks', values.ownMask, values.prototypeMask,
                values[ownSymbol], values[prototypeSymbol], reads, writes);
            console.log('visible', values.visible, reads, writes);
            console.log('flags', Object.getOwnPropertyDescriptor(values, 'ownMask')!.enumerable,
                Object.getOwnPropertyDescriptor(values, ownSymbol)!.enumerable,
                Object.getOwnPropertyDescriptor(prototype, 'prototypeMask')!.enumerable,
                Object.getOwnPropertyDescriptor(prototype, prototypeSymbol)!.enumerable);
            """, "presence true true true true true true false 0 0\nmasks undefined undefined undefined undefined 0 0\nvisible 7 1 0\nflags false false false false\n"];
        yield return ["array-prototype-getter-receiver", """
            class Values extends Array {}
            const values: any = new Values(3, 4), state = { gets: 0 };
            Object.defineProperty(Array.prototype, "watched", {
                get: function(this: any) { state.gets++; return this[0]; }, configurable: true
            });
            try {
                console.log(values.watched, values["watched"],
                    Reflect.get(values, "watched", { 0: 30 }), state.gets);
            } finally {
                delete (Array.prototype as any).watched;
            }
            """, "3 3 30 3\n"];
        yield return ["native-array-method-receivers", """
            class Values extends Array {}
            const values: any = new Values(3, 4);
            const mapped: any = values.map((value: any) => value + 1);
            const filtered: any = values.filter((value: any) => value > 3);
            const iterator: any = values.values();
            console.log(mapped.join(","), filtered.join(","),
                iterator.next().value, iterator.next().value, iterator.next().done);
            console.log(values.length, values[0], values[1]);
            """, "4,5 4 3 4 true\n2 3 4\n"];
        yield return ["intrinsic-inherited-presence", """
            class Values extends Array {}
            const values: any = new Values();
            console.log("hasOwnProperty" in [], "valueOf" in values, "push" in values,
                Symbol.iterator in values);
            Object.setPrototypeOf(values, null);
            console.log("hasOwnProperty" in values, "valueOf" in values, "push" in values,
                Symbol.iterator in values);
            """, "true true true true\nfalse false false false\n"];
        yield return ["array-prototype-custom-chain", """
            class Values extends Array {}
            const values: any = new Values(4, 5);
            const custom: any = Object.create(Array.prototype);
            custom.marker = 17;
            Object.setPrototypeOf(values, custom);
            console.log(values instanceof Values, values instanceof Array, values instanceof Object);
            """, "false true true\n"];
        yield return ["array-prototype-custom-properties", """
            class Values extends Array {}
            const values: any = new Values(4, 5);
            const custom: any = Object.create(Array.prototype);
            custom.marker = 17;
            Object.setPrototypeOf(values, custom);
            console.log(values.marker, values.push(6), values[2], Array.isArray(values));
            """, "17 3 6 true\n"];
        yield return ["nonextensible-same-prototype", """
            class Values extends Array {}
            const values: any = new Values(1, 2);
            const original = Object.getPrototypeOf(values);
            Object.preventExtensions(values);
            console.log(Object.setPrototypeOf(values, original) === values, Reflect.setPrototypeOf(values, original));
            console.log(Reflect.setPrototypeOf(values, null));
            try { Object.setPrototypeOf(values, null); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(values) === original, values.length, values[0]);
            """, "true true\nfalse\ntrue\ntrue 2 1\n"];
        yield return ["noncanonical-index-fields", """
            class Values extends Array {
                '01' = 'own';
                '+0' = 'plus';
                '-0' = 'minus';
                '4294967295' = 'max';
            }
            const values: any = new Values(10, 20);
            console.log(values[0], values[1], values['01'], values['+0'], values['-0'], values['4294967295']);
            Object.setPrototypeOf(values, null);
            console.log(values[0], values[1], values['01'], values['+0'], values['-0'], values['4294967295']);
            """, "10 20 own plus minus max\n10 20 own plus minus max\n"];
        yield return ["cycle-rejection", """
            class Values extends Array {}
            const values: any = new Values();
            const original = Object.getPrototypeOf(values);
            console.log(Reflect.setPrototypeOf(values, values));
            const custom: any = {};
            Object.setPrototypeOf(custom, values);
            console.log(Reflect.setPrototypeOf(values, custom));
            try { Object.setPrototypeOf(values, custom); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(values) === original);
            """, "false\nfalse\ntrue\ntrue\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // Compiled counterparts are tracked independently in #2602, #2603 and #2605.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] is not
                ("arguments-membership" or "live-prototype-membership" or "constructor-prototype-identity"))
            // #2423 tracks compiled method overrides; #2606 tracks removed native ancestors.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] != "live-prototype-methods")
            // #2609 omits arguments' own iterator; #2611 invents Object.prototype presence.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] is not
                ("arguments-own-iterator" or "intrinsic-inherited-presence"))
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ArrayStorageSurvivesLegalPrototypeMutation(string name, string source, string expected, ExecutionMode mode)
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
