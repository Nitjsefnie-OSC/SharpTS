using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OrdinaryFunctionGetterReceiverTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact", """
            'use strict';
            const owner: any = function() {};
            Object.defineProperty(owner, "value", {
                get: function(this: any) { return this; }, configurable: true
            });
            console.log(owner.value === owner);
            """, "true\n"];

        yield return ["string-and-symbol-getter-receivers", """
            'use strict';
            function declaration() {}
            const expression = function() {}, arrow = () => {};
            const other = {}, key = Symbol('receiver'), state = { gets: 0 };
            function declaredGetter(this: any) { state.gets++; return this; }
            const expressionGetter = function(this: any) { state.gets++; return this; };
            for (const getter of [declaredGetter, expressionGetter]) {
                for (const owner of [declaration, expression, arrow] as any[]) {
                    Object.defineProperty(owner, 'value', { get: getter, configurable: true });
                    Object.defineProperty(owner, key, { get: getter, configurable: true });
                    const before = state.gets;
                    console.log(owner.value === owner, owner['value'] === owner,
                        Reflect.get(owner, 'value', other) === other,
                        owner[key] === owner, Reflect.get(owner, key, other) === other,
                        state.gets - before, Object.getOwnPropertyDescriptor(owner, 'value')!.get === getter);
                }
            }
            """, string.Concat(Enumerable.Repeat("true true true true true 5 true\n", 6))];

        yield return ["internal-array-like-property-reads", """
            'use strict';
            function declaration() {}
            const expression = function() {}, arrow = () => {};
            for (const owner of [declaration, expression, arrow] as any[]) {
                const state = { lengths: 0, indices: 0, receivers: true };
                Object.defineProperty(owner, 'length', {
                    get: function(this: any) {
                        state.lengths++; state.receivers = state.receivers && this === owner; return 1;
                    }, configurable: true
                });
                Object.defineProperty(owner, '0', {
                    get: function(this: any) {
                        state.indices++; state.receivers = state.receivers && this === owner; return 'item';
                    }, configurable: true
                });
                const result = Array.prototype.join.call(owner, ',');
                console.log(result === 'item',
                    state.lengths, state.indices, state.receivers);
            }
            """, string.Concat(Enumerable.Repeat("true 1 1 true\n", 3))];

        yield return ["lexical-getter-controls", """
            'use strict';
            function declaration() {}
            const expression = function() {}, arrow = () => {};
            const lexicalReceiver = {}, other = {}, state = { calls: 0 };
            function makeLexical(this: any) { return () => { state.calls++; return this; }; }
            const lexical = makeLexical.call(lexicalReceiver);
            for (const owner of [declaration, expression, arrow] as any[]) {
                Object.defineProperty(owner, 'lexical', { get: lexical, configurable: true });
                const before = state.calls;
                console.log(owner.lexical === lexicalReceiver, owner['lexical'] === lexicalReceiver,
                    Reflect.get(owner, 'lexical', other) === lexicalReceiver, state.calls - before);
            }
            """, string.Concat(Enumerable.Repeat("true true true 3\n", 3))];

        yield return ["bound-getter-controls", """
            'use strict';
            function declaration() {}
            const expression = function() {}, arrow = () => {};
            const boundReceiver = {}, other = {}, state = { calls: 0 };
            const bound = function(this: any) { state.calls++; return this; }.bind(boundReceiver);
            for (const owner of [declaration, expression, arrow] as any[]) {
                Object.defineProperty(owner, 'bound', { get: bound, configurable: true });
                const before = state.calls;
                console.log(owner.bound === boundReceiver, owner['bound'] === boundReceiver,
                    Reflect.get(owner, 'bound', other) === boundReceiver, state.calls - before);
            }
            """, string.Concat(Enumerable.Repeat("true true true 3\n", 3))];

        yield return ["return-and-abrupt-identity", """
            'use strict';
            function declaration() {}
            const expression = function() {}, arrow = () => {}, token = {}, marker = {};
            for (const owner of [declaration, expression, arrow] as any[]) {
                const state = { calls: 0, receivers: true, noArguments: true };
                const getter = function(this: any, ...args: any[]) {
                    state.calls++; state.receivers = state.receivers && this === owner;
                    state.noArguments = state.noArguments && args.length === 0; return token;
                };
                function fail(this: any) {
                    state.calls++; state.receivers = state.receivers && this === owner; throw marker;
                }
                Object.defineProperty(owner, 'value', { get: getter, configurable: true });
                Object.defineProperty(owner, 'failure', { get: fail, configurable: true });
                console.log(owner.value === token, owner['value'] === token);
                try { owner.failure; } catch (error) { console.log(error === marker); }
                try { owner['failure']; } catch (error) { console.log(error === marker); }
                console.log(state.calls, state.receivers, state.noArguments);
                console.log(Reflect.get(owner, 'value') === token, state.calls, state.receivers);
            }
            """, string.Concat(Enumerable.Repeat(
                "true true\ntrue\ntrue\n4 true true\ntrue 5 true\n", 3))];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            // #2665: compiled descriptor conversion rejects a callable bound getter.
            if ((string)row[0] == "bound-getter-controls") continue;
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void OwnFunctionGettersPreserveTheirReceiver(
        string name, string source, string expected, ExecutionMode mode)
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
