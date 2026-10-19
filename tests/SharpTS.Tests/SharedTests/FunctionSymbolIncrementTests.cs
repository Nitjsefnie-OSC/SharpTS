using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionSymbolIncrementTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-four-forms-and-key-identity", """
            function C() {}
            const target: any = C, key = Symbol('count'), other = Symbol('count');
            target[other] = 17; target['Symbol(count)'] = 19;
            target[key] = 1; console.log(target[key]++, target[key]);
            target[key] = 1; console.log(++target[key], target[key]);
            target[key] = 1; console.log(target[key]--, target[key]);
            target[key] = 1; console.log(--target[key], target[key]);
            target['count'] = 3;
            console.log(target['count']++, target['count'], target[other], target['Symbol(count)']);
            """, "1 2\n2 2\n1 0\n0 0\n3 4 17 19\n"];
        yield return ["function-forms", """
            function declaration() {}
            function* generatorDeclaration() { yield 1; }
            async function* asyncGeneratorDeclaration() { yield 1; }
            const functions: any[] = [declaration, function() {}, () => {}, declaration.bind(null),
                generatorDeclaration, function*() { yield 1; },
                asyncGeneratorDeclaration, async function*() { yield 1; }];
            for (const target of functions) {
                const key = Symbol(); target[key] = 1;
                const post = target[key]++, pre = ++target[key];
                const down = target[key]--, last = --target[key];
                console.log(post, pre, down, last, target[key]);
            }
            """, string.Concat(Enumerable.Repeat("1 3 3 1 1\n", 8))];
        yield return ["async-function-forms", """
            async function declaration() {}
            const functions: any[] = [declaration, async function() {}, async () => {}];
            for (const target of functions) {
                const key = Symbol(); target[key] = 1;
                const post = target[key]++, pre = ++target[key];
                const down = target[key]--, last = --target[key];
                console.log(post, pre, down, last, target[key]);
            }
            """, string.Concat(Enumerable.Repeat("1 3 3 1 1\n", 3))];
        yield return ["own-accessor-order-receiver-and-abrupt", """
            function C() {}
            const target: any = C, key = Symbol(), token: any = {};
            const events: string[] = [], state = { value: '4', rightThis: true, writes: 0 };
            const getter = function(this: any) { events.push('g'); state.rightThis = state.rightThis && this === target; return state.value; };
            const setter = function(this: any, value: any) { events.push('s'); state.rightThis = state.rightThis && this === target; state.value = value; };
            Object.defineProperty(target, key, { get: getter, set: setter, configurable: true, enumerable: true });
            function receiver(): any { events.push('r'); return target; }
            function property(): any { events.push('k'); return key; }
            const old = receiver()[property()]++;
            const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
            console.log(old, typeof old, state.value, events.join(''), state.rightThis,
                descriptor.get === getter, descriptor.set === setter, descriptor.enumerable, descriptor.configurable);
            Object.defineProperty(target, key, { get() { throw token; }, set(value: any) { state.writes++; } });
            try { target[key]++; } catch (error) { console.log(error === token, state.writes); }
            Object.defineProperty(target, key, { get() { return 1; }, set(value: any) { state.writes++; throw token; } });
            try { ++target[key]; } catch (error) { console.log(error === token, state.writes); }
            function badKey(): any { throw token; }
            try { target[badKey()]++; } catch (error) { console.log(error === token, state.writes); }
            """, "4 number 5 rkgs true true true true true\ntrue 0\ntrue 1\ntrue 1\n"];
        yield return ["readonly-strict-and-integrity", """
            function C() {}
            const target: any = C, locked = Symbol(), getterOnly = Symbol(), writable = Symbol(), accessor = Symbol(), missing = Symbol();
            const state = { value: 2, calls: 0 };
            function strictIncrement(value: any, key: any) { 'use strict'; return ++value[key]; }
            Object.defineProperty(target, locked, { value: 4 });
            Object.defineProperty(target, getterOnly, { get() { return 9; } });
            console.log(target[locked]++, ++target[locked], target[locked]);
            console.log(++target[getterOnly], target[getterOnly]--, target[getterOnly]);
            try { strictIncrement(target, locked); } catch (error) { console.log(error instanceof TypeError); }
            try { strictIncrement(target, getterOnly); } catch (error) { console.log(error instanceof TypeError); }
            target[writable] = 3;
            Object.defineProperty(target, accessor, { get() { return state.value; }, set(value: number) { state.value = value; state.calls++; } });
            Object.preventExtensions(target);
            console.log(target[writable]++, target[writable], ++target[accessor], state.value, state.calls);
            console.log(Number.isNaN(target[missing]++), Object.hasOwn(target, missing));
            try { strictIncrement(target, missing); } catch (error) { console.log(error instanceof TypeError); }
            Object.freeze(target);
            console.log(Object.isFrozen(target), target[writable]++, target[writable]);
            try { strictIncrement(target, writable); } catch (error) { console.log(error instanceof TypeError); }
            """, "4 5 4\n10 9 9\ntrue\ntrue\n3 4 3 3 1\ntrue false\ntrue\ntrue 4 4\ntrue\n"];
        yield return ["inherited-prototype-descriptors", """
            function C() {}
            const target: any = C, shared: any = Function.prototype, key = Symbol();
            const state = { value: 0, reads: 0, rightThis: true, masked: false };
            Object.defineProperty(shared, key, {
                get: function(this: any) { state.reads++; state.rightThis = state.rightThis && this === target; return '4'; },
                set: function(this: any, value: number) { state.rightThis = state.rightThis && this === target; state.value = value; }, configurable: true
            });
            console.log(target[key]++, state.value, state.reads, state.rightThis, Object.hasOwn(target, key));
            delete shared[key];
            const custom: any = Object.create(Function.prototype);
            Object.defineProperty(custom, key, { value: 7, writable: true, configurable: true });
            Object.setPrototypeOf(target, custom);
            console.log(++target[key], target[key], custom[key], Object.hasOwn(target, key));
            delete target[key];
            Object.defineProperty(custom, key, { value: 5, writable: false });
            console.log(target[key]++, target[key], Object.hasOwn(target, key));
            Object.defineProperty(custom, key, { get() { state.reads++; return 11; }, set(value: number) { state.value = value; } });
            Object.defineProperty(target, key, { set(value: number) { state.masked = Number.isNaN(value); }, configurable: true });
            console.log(Number.isNaN(target[key]++), state.masked, state.reads, state.value);
            """, "4 5 1 true false\n8 8 7 true\n5 5 false\ntrue true 1 5\n"];
        yield return ["awaited-receiver-and-key", """
            function C() {}
            const target: any = C, key = Symbol(), events: string[] = [];
            target[key] = 5;
            async function receiver(): Promise<any> { events.push('r'); return target; }
            async function property(): Promise<any> { events.push('k'); return key; }
            async function main() {
                const old = (await receiver())[await property()]++;
                const next = --(await receiver())[await property()];
                console.log(old, next, target[key], events.join(''));
            }
            main();
            """, "5 5 5 rkrk\n"];
        yield return ["object-to-symbol-key", """
            function C() {}
            const functions: any[] = [Math.abs, C];
            for (const target of functions) {
                const first = Symbol('first'), second = Symbol('second'), token: any = {}, events: string[] = [];
                const state = { calls: 0, value: 9, getterThis: false, setterThis: false };
                Object.defineProperty(target, first, {
                    get: function(this: any) { events.push('g'); state.getterThis = this === target; return 5; },
                    set(value: number) { events.push('wrong'); }, configurable: true
                });
                Object.defineProperty(target, second, {
                    set: function(this: any, value: number) { events.push('s'); state.setterThis = this === target; state.value = value; }, configurable: true
                });
                const property: any = { [Symbol.toPrimitive](hint: string) {
                    state.calls++; events.push('k' + state.calls + ':' + hint);
                    return state.calls === 1 ? first : second;
                } };
                function key(): any { events.push('p'); return property; }
                const old = target[key()]++;
                console.log(old, state.value, state.calls, state.getterThis, state.setterThis, events.join(','));
                const bad: any = { [Symbol.toPrimitive](hint: string) { events.push('throw'); throw token; } };
                try { ++target[bad]; } catch (error) { console.log(error === token, state.value, events.join(',')); }
            }
            """, string.Concat(Enumerable.Repeat("5 6 2 true true p,k1:string,g,k2:string,s\ntrue 6 p,k1:string,g,k2:string,s,throw\n", 2))];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // #2595: compiled updates reject an object key producing a Symbol.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] != "object-to-symbol-key")
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void FunctionUpdatesUseOrdinarySymbolReferences(string name, string source, string expected, ExecutionMode mode)
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
