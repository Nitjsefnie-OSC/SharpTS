using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionReadonlyPropertyTests
{
    public const string IssueSource = """
        const C: any = function() {};
        Object.defineProperty(C, 'value', { set: undefined, configurable: true });
        function write(target: any) { 'use strict'; target.value = 7; }
        try { write(C); console.log(false); }
        catch (error) { console.log(error instanceof TypeError); }
        """ + "\n";

    public const string GetterOnlyLifecycleSource = """
        function declared() {}
        const owners: any[] = [declared, function() {}, () => {}];
        function receiver(state: any): any { state.receivers++; return state.target; }
        function key(state: any): string { state.keys++; return "value"; }
        function rhs(state: any): number { state.rhs++; return 7; }
        function sloppyDot(state: any) { return receiver(state).value = rhs(state); }
        function sloppyIndex(state: any) { return receiver(state)[key(state)] = rhs(state); }
        function strictDot(state: any) { "use strict"; return receiver(state).value = rhs(state); }
        function strictIndex(state: any) { "use strict"; return receiver(state)[key(state)] = rhs(state); }
        for (const owner of owners) {
            for (const explicitSetter of [false, true]) {
                const state = { target: owner, gets: 0, receivers: 0, keys: 0, rhs: 0 };
                const getter = function(this: any) { state.gets++; return 21; };
                const shape: any = { get: getter, enumerable: true, configurable: true };
                if (explicitSetter) shape.set = undefined;
                Object.defineProperty(owner, "value", shape);
                console.log(sloppyDot(state), sloppyIndex(state), state.gets,
                    state.receivers, state.keys, state.rhs);
                try { strictDot(state); console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
                try { strictIndex(state); console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
                const descriptor = Object.getOwnPropertyDescriptor(owner, "value")!;
                console.log(state.gets, state.receivers, state.keys, state.rhs,
                    descriptor.get === getter, descriptor.set === undefined,
                    Object.hasOwn(descriptor, "value"), Object.hasOwn(descriptor, "writable"),
                    descriptor.enumerable, descriptor.configurable);
                console.log(owner.value, state.gets);
                delete owner.value;
            }
        }
        """ + "\n";

    public const string CallableSettersSource = """
        function declared() {}
        const owners: any[] = [declared, function() {}, () => {}];
        function strictDot(target: any, value: number) { "use strict"; return target.value = value; }
        function strictIndex(target: any, value: number) { "use strict"; return target["value"] = value; }
        for (const owner of owners) {
            const state = { gets: 0, sets: 0, last: 0, same: true };
            const getter = function() { state.gets++; return 13; };
            const setter = function(this: any, value: number) {
                state.sets++; state.last = value; state.same = state.same && this === owner;
                return 999;
            };
            Object.defineProperty(owner, "value", { get: getter, set: setter, configurable: true });
            console.log(owner.value = 3, strictIndex(owner, 4), state.sets,
                state.last, state.same, state.gets);
            const marker = {};
            const throwing = function(this: any, value: number) {
                state.sets++; state.same = state.same && this === owner;
                throw marker;
            };
            Object.defineProperty(owner, "value", { set: throwing });
            try { strictDot(owner, 5); console.log(false); }
            catch (error) { console.log(error === marker); }
            try { strictIndex(owner, 6); console.log(false); }
            catch (error) { console.log(error === marker); }
            const descriptor = Object.getOwnPropertyDescriptor(owner, "value")!;
            console.log(state.sets, state.same, state.gets, descriptor.get === getter,
                descriptor.set === throwing, Object.hasOwn(descriptor, "value"));
        }
        """ + "\n";

    public const string ReadonlyDataSource = """
        function declared() {}
        const owners: any[] = [declared, function() {}, () => {}, (function() {}).bind(null)];
        function strictDot(target: any) { "use strict"; return target.fixed = 7; }
        function strictIndex(target: any) { "use strict"; return target["fixed"] = 8; }
        function strictField(target: any, value: any) { "use strict"; return target.callback = value; }
        function strictSymbol(target: any, key: symbol) { "use strict"; target[key] = 10; }
        const key = Symbol("own");
        for (const owner of owners) {
            Object.defineProperty(owner, "fixed", { value: 9, writable: false, configurable: true });
            console.log(owner.fixed = 5, owner["fixed"] = 6, owner.fixed);
            try { strictDot(owner); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            try { strictIndex(owner); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            const descriptor = Object.getOwnPropertyDescriptor(owner, "fixed")!;
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            const first = () => 1, replacement = () => 2;
            owner.callback = first;
            console.log(owner.callback === first, strictField(owner, replacement) === replacement,
                owner.callback === replacement, owner.callback());
            Object.defineProperty(owner, key, { set: undefined, configurable: true });
            owner[key] = 11;
            try { strictSymbol(owner, key); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(owner[key] === undefined, Object.getOwnPropertyDescriptor(owner, key)!.set === undefined);
        }
        """ + "\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true\n"];
        yield return ["getter-only-lifecycle-and-order", GetterOnlyLifecycleSource,
            string.Concat(Enumerable.Repeat("7 7 0 2 1 2\ntrue\ntrue\n0 4 2 4 true true false false true true\n21 1\n", 6))];
        yield return ["live-setters-receiver-and-abrupt", CallableSettersSource,
            string.Concat(Enumerable.Repeat("3 4 2 4 true 0\ntrue\ntrue\n4 true 0 true true false\n", 3))];
        yield return ["readonly-data-and-callable-controls", ReadonlyDataSource,
            string.Concat(Enumerable.Repeat("5 6 9\ntrue\ntrue\n9 false false true\ntrue true true 2\ntrue\ntrue true\n", 4))];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OwnCallableDescriptorsRespectStrictWritesAndLiveSetters(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
