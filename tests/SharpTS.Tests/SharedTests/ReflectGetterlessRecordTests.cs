using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ReflectGetterlessRecordTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            const state = { reads: 0 }, prototype: any = {};
            Object.defineProperty(prototype, 'x', { get() { state.reads++; return 7; }, configurable: true });
            const target: any = Object.create(prototype);
            Object.defineProperty(target, 'x', { get: undefined, configurable: true });
            console.log(target.x === undefined, Reflect.get(target, 'x') === undefined, state.reads);
            """, "true true 0\n"];
        yield return ["accessor-and-data-masks", """
            const state = { reads: 0, writes: 0 }, prototype: any = {};
            Object.defineProperty(prototype, 'x', { get() { state.reads++; return 7; }, configurable: true });
            const shapes: any[] = [{ get: undefined }, { set: undefined }, { get: undefined, set: undefined },
                { set(value: any) { state.writes++; } }, { value: undefined }, {}];
            for (const shape of shapes) {
                const target: any = Object.create(prototype);
                Object.defineProperty(target, 'x', Object.assign({ configurable: true }, shape));
                console.log(target.x === undefined, Reflect.get(target, 'x') === undefined,
                    Reflect.get(target, 'x', {}) === undefined, state.reads, state.writes);
            }
            const target: any = Object.create(prototype);
            Object.defineProperty(target, 'x', { value: null });
            console.log(Reflect.get(target, 'x') === null, state.reads, state.writes);
            """, string.Concat(Enumerable.Repeat("true true true 0 0\n", 6)) + "true 0 0\n"];
        yield return ["receiver-getters-and-abrupt-values", """
            const state: any = { own: 0, inherited: 0, last: undefined }, prototype: any = {}, marker = {};
            Object.defineProperty(prototype, 'x', { get: function(this: any) {
                state.inherited++; state.last = this; return this.tag;
            }, configurable: true });
            const target: any = Object.create(prototype), receiver = { tag: {} };
            target.tag = {};
            const getter = function(this: any) { state.own++; state.last = this; return this.tag; };
            Object.defineProperty(target, 'x', { get: getter, configurable: true });
            console.log(Reflect.get(target, 'x') === target.tag, state.own, state.last === target,
                Object.getOwnPropertyDescriptor(target, 'x')!.get === getter);
            console.log(Reflect.get(target, 'x', receiver) === receiver.tag, state.own, state.last === receiver);
            Object.defineProperty(target, 'x', { get() { state.own++; throw marker; } });
            try { Reflect.get(target, 'x', receiver); } catch (error) {
                console.log(error === marker, state.own, state.inherited);
            }
            Object.defineProperty(target, 'x', { get: undefined });
            console.log(Reflect.get(target, 'x', receiver) === undefined, state.own, state.inherited);
            delete target.x;
            console.log(Reflect.get(target, 'x', receiver) === receiver.tag, state.inherited, state.last === receiver);
            """, "true 1 true true\ntrue 2 true\ntrue 3 0\ntrue 3 0\ntrue 1 true\n"];
        yield return ["mask-stops-before-proxy-prototype", """
            const state: any = { gets: 0, receiver: undefined }, receiver = {};
            const prototype = new Proxy({}, { get(target: any, key: any, actual: any) {
                if (key === 'x') { state.gets++; state.receiver = actual; return 7; }
                return Reflect.get(target, key, actual);
            } });
            const target: any = Object.create(prototype);
            Object.defineProperty(target, 'x', { get: undefined, configurable: true });
            console.log(Reflect.get(target, 'x') === undefined, Reflect.get(target, 'x', receiver) === undefined, state.gets);
            delete target.x;
            console.log(Reflect.get(target, 'x', receiver), state.gets, state.receiver === receiver);
            Object.defineProperty(target, 'x', { value: undefined, configurable: true });
            console.log(Reflect.get(target, 'x') === undefined, state.gets);
            """, "true true 0\n7 1 true\ntrue 1\n"];
        // #2646: inline awaited Reflect.get arguments fail compiler IL verification.
        yield return ["awaited-reference-and-key-coercion", """
            const state = { targets: 0, keys: 0, receivers: 0, conversions: 0, hint: '', reads: 0 };
            const prototype: any = {};
            Object.defineProperty(prototype, 'x', { get() { state.reads++; return 7; } });
            const target: any = Object.create(prototype);
            Object.defineProperty(target, 'x', { get: undefined });
            const property: any = { [Symbol.toPrimitive](hint: string) { state.conversions++; state.hint = hint; return 'x'; } };
            async function getTarget(): Promise<any> { state.targets++; return target; }
            async function getKey(): Promise<any> { state.keys++; return property; }
            async function getReceiver(): Promise<any> { state.receivers++; return {}; }
            async function run() {
                const get: any = Reflect.get;
                const selectedTarget = await getTarget(), selectedKey = await getKey(), selectedReceiver = await getReceiver();
                const value = get(selectedTarget, selectedKey, selectedReceiver);
                console.log(value === undefined, state.targets, state.keys, state.receivers, state.conversions, state.hint, state.reads);
            }
            run().catch((error: any) => console.log('unexpected', error.name, error.message));
            """, "true 1 1 1 1 string 0\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OwnGetterlessRecordsStopReflectLookup(string name, string source, string expected, ExecutionMode mode)
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
