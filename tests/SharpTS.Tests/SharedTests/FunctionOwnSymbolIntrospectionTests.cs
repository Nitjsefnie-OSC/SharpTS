using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionOwnSymbolIntrospectionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            function F() {}
            const functions: any[] = [F, function Expression() {}, () => {}];
            for (const target of functions) {
                const key = Symbol('own');
                Object.defineProperty(target, key, { value: 1, writable: true, configurable: true });
                console.log(target[key], Object.hasOwn(target, key), Object.getOwnPropertyDescriptor(target, key)?.value,
                    Object.getOwnPropertySymbols(target).includes(key), Reflect.ownKeys(target).includes(key));
            }
            """, "1 true 1 true true\n1 true 1 true true\n1 true 1 true true\n"];
        yield return ["assignment-and-definition-flags", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            for (const target of functions) {
                const assigned = Symbol(), defined = Symbol();
                target[assigned] = 7;
                Object.defineProperty(target, defined, { value: 9 });
                const first: any = Object.getOwnPropertyDescriptor(target, assigned);
                const second: any = Object.getOwnPropertyDescriptor(target, defined);
                console.log(first?.value, first?.writable, first?.enumerable, first?.configurable);
                console.log(second?.value, second?.writable, second?.enumerable, second?.configurable);
                console.log(target.hasOwnProperty(assigned), Object.keys(target).length);
            }
            """, "7 true true true\n9 false false false\ntrue 0\n7 true true true\n9 false false false\ntrue 0\n7 true true true\n9 false false false\ntrue 0\n"];
        yield return ["accessor-descriptor-does-not-call-getter", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                const state = { reads: 0 };
                const getter = function() { state.reads++; return 11; };
                const setter = function(value: number) {};
                Object.defineProperty(target, key, { get: getter, set: setter, enumerable: true, configurable: true });
                const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
                console.log(descriptor?.get === getter, descriptor?.set === setter, descriptor?.enumerable, descriptor?.configurable);
                console.log(descriptor !== undefined && !('value' in descriptor), Object.hasOwn(target, key),
                    Object.getOwnPropertySymbols(target)[0] === key, state.reads);
            }
            """, "true true true true\ntrue true true 0\ntrue true true true\ntrue true true 0\ntrue true true true\ntrue true true 0\n"];
        yield return ["symbol-identity-and-order", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            for (const target of functions) {
                const first = Symbol('same'), second = Symbol('same'), third = Symbol('same');
                target['Symbol(same)'] = 'text';
                Object.defineProperty(target, first, { get() { throw 1; }, configurable: true });
                target[second] = 2;
                Object.defineProperty(target, third, { get() { throw 1; }, configurable: true });
                Object.defineProperty(target, first, { value: 11, writable: true });
                const symbols = Object.getOwnPropertySymbols(target);
                const keys = Reflect.ownKeys(target), start = keys.length - 3;
                console.log(symbols.length, symbols[0] === first, symbols[1] === second, symbols[2] === third);
                console.log(keys[start] === first, keys[start + 1] === second, keys[start + 2] === third, target['Symbol(same)']);
            }
            """, "3 true true true\ntrue true true text\n3 true true true\ntrue true true text\n3 true true true\ntrue true true text\n"];
        yield return ["aggregate-and-reflect-descriptors", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            for (const target of functions) {
                const data = Symbol(), accessor = Symbol();
                const state = { reads: 0 };
                const getter = function() { state.reads++; return 11; };
                target[data] = 7;
                Object.defineProperty(target, accessor, { get: getter, configurable: true });
                const descriptors: any = Object.getOwnPropertyDescriptors(target);
                console.log(descriptors[data]?.value, descriptors[accessor]?.get === getter,
                    Reflect.getOwnPropertyDescriptor(target, data)?.value, state.reads);
            }
            """, "7 true 7 0\n7 true 7 0\n7 true 7 0\n"];
        yield return ["reflect-symbol-deletion", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                target[key] = 7;
                console.log(Reflect.deleteProperty(target, key), Object.hasOwn(target, key),
                    Object.getOwnPropertySymbols(target).length, target[key] === undefined);
            }
            """, "true false 0 true\ntrue false 0 true\ntrue false 0 true\n"];
        yield return ["descriptor-kind-transition", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                Object.defineProperty(target, key, { get() { throw 1; }, configurable: true });
                Object.defineProperty(target, key, { value: undefined, writable: true });
                const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
                console.log(descriptor?.value === undefined, descriptor?.writable, descriptor?.enumerable, descriptor?.configurable);
                console.log(descriptor !== undefined && !('get' in descriptor), Object.getOwnPropertySymbols(target).length, target[key] === undefined);
            }
            """, "true true false true\ntrue 1 true\ntrue true false true\ntrue 1 true\ntrue true false true\ntrue 1 true\n"];
        yield return ["ordinary-object-writable-conversion-2369", """
            const target: any = {}, key = Symbol();
            Object.defineProperty(target, key, { get() { return 1; }, enumerable: true, configurable: true });
            Object.defineProperty(target, key, { writable: true });
            const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
            console.log(Object.hasOwn(target, key), Object.getOwnPropertySymbols(target).includes(key), target[key] === undefined);
            console.log(descriptor !== undefined, descriptor?.value === undefined, descriptor?.writable, descriptor?.enumerable, descriptor?.configurable);
            """, "true true true\ntrue true true true true\n"];
        yield return ["transition-resets-writable", """
            function F() {}
            const targets: any[] = [{}, F, function() {}, () => {}];
            for (const target of targets) {
                const key = Symbol();
                target[key] = 1;
                Object.defineProperty(target, key, { get() { return 2; } });
                Object.defineProperty(target, key, { value: 3 });
                const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
                console.log(descriptor?.value, descriptor?.writable, descriptor?.enumerable, descriptor?.configurable);
            }
            """, "3 false true true\n3 false true true\n3 false true true\n3 false true true\n"];
        yield return ["delete-and-readd", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            for (const target of functions) {
                const first = Symbol(), second = Symbol();
                target[first] = 1; target[second] = 2;
                console.log(delete target[first], Object.hasOwn(target, first), Object.getOwnPropertySymbols(target).length);
                target[first] = 3;
                const symbols = Object.getOwnPropertySymbols(target);
                console.log(symbols[0] === second, symbols[1] === first, target[first]);
            }
            """, "true false 1\ntrue true 3\ntrue false 1\ntrue true 3\ntrue false 1\ntrue true 3\n"];
        yield return ["frozen-symbol-integrity", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            function strictWrite(target: any, key: symbol) { 'use strict'; target[key] = 9; }
            for (const target of functions) {
                const key = Symbol();
                target[key] = 7;
                Object.freeze(target);
                target[key] = 9;
                const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
                console.log(target[key], descriptor?.writable, descriptor?.configurable, Object.isFrozen(target));
                console.log(delete target[key], Object.hasOwn(target, key));
                try { strictWrite(target, key); } catch (error) { console.log(error instanceof TypeError); }
            }
            """, "7 false false true\nfalse true\ntrue\n7 false false true\nfalse true\ntrue\n7 false false true\nfalse true\ntrue\n"];
        yield return ["frozen-noop-redefinitions", """
            function F() {}
            const targets: any[] = [{}, F, function() {}, () => {}];
            for (const target of targets) {
                const key = Symbol();
                target[key] = 7;
                Object.freeze(target);
                console.log(Object.defineProperty(target, key, { value: 7 }) === target,
                    Object.defineProperty(target, key, {}) === target);
                console.log(target[key]);
            }
            """, "true true\n7\ntrue true\n7\ntrue true\n7\ntrue true\n7\n"];
        yield return ["frozen-redefinition-validation", """
            function F() {}
            const targets: any[] = [{}, F, function() {}, () => {}];
            for (const target of targets) {
                const key = Symbol();
                target[key] = 7;
                Object.freeze(target);
                try { Object.defineProperty(target, key, { value: 8 }); }
                catch (error) { console.log(error instanceof TypeError); }
                try { Object.defineProperty(target, Symbol(), { value: 9 }); }
                catch (error) { console.log(error instanceof TypeError); }
                console.log(target[key]);
            }
            """, "true\ntrue\n7\ntrue\ntrue\n7\ntrue\ntrue\n7\ntrue\ntrue\n7\n"];
        yield return ["undefined-accessor-is-own", """
            function F() {}
            const functions: any[] = [F, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                const parent: any = { [key]: 17 };
                Object.setPrototypeOf(target, parent);
                Object.defineProperty(target, key, { get: undefined, configurable: true });
                const descriptor: any = Object.getOwnPropertyDescriptor(target, key);
                console.log(target[key] === undefined, Object.hasOwn(target, key),
                    descriptor !== undefined && 'get' in descriptor && !('value' in descriptor));
                console.log(delete target[key], Reflect.get(target, key));
            }
            """, "true true true\ntrue 17\ntrue true true\ntrue 17\ntrue true true\ntrue 17\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // Compiled Reflect Symbol deletion and frozen redefinition validation are tracked in #2160/#2372.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] is not
                ("reflect-symbol-deletion" or "frozen-redefinition-validation"))
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void OwnSymbolIntrospectionAgreesWithFunctionStorage(string name, string source, string expected, ExecutionMode mode)
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
