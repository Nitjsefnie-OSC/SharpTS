using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ObjectConstructorAccessorReadTests
{
    public const string SetterOnlySource = """
        const O: any = Object;
        let writes = 0;
        Object.defineProperty(O, "keys", { set: () => { writes++; }, configurable: true });
        console.log("intrinsic", O.keys === undefined, O["keys"] === undefined,
            Reflect.get(O, "keys") === undefined, typeof O.keys);
        Object.defineProperty(O, "custom", { set: () => { writes++; }, configurable: true });
        console.log("custom", O.custom === undefined, O["custom"] === undefined,
            Reflect.get(O, "custom") === undefined, O.custom === null, O["custom"] === null);
        console.log("writes", writes);
        """;

    public const string UndefinedGetterSource = """
        const O: any = Object, state = { writes: 0, gets: 0, valid: true };
        Object.defineProperty(O, "keys", {
            get: undefined, set: () => { state.writes++; }, configurable: true
        });
        Object.defineProperty(O, "custom", { get: undefined, configurable: true });
        console.log(O.keys === undefined, O["keys"] === undefined,
            Reflect.get(O, "keys") === undefined, Object.getOwnPropertyDescriptor(O, "keys")!.get === undefined);
        console.log(O.custom === undefined, O["custom"] === undefined,
            Reflect.get(O, "custom") === undefined, O.custom === null, state.writes);
        Object.defineProperty(O, "custom", {
            get() { state.gets++; state.valid = state.valid && this === O; return null; }, configurable: true
        });
        console.log(O.custom === null, O["custom"] === null, Reflect.get(O, "custom") === null,
            state.gets, state.valid, state.writes);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["getterless-issue", SetterOnlySource,
            "intrinsic true true true undefined\ncustom true true true false false\nwrites 0\n"];
        yield return ["undefined-getter", UndefinedGetterSource,
            "true true true true\ntrue true true false 0\ntrue true true 3 true 0\n"];
        yield return ["issue", """
            const O: any = Object;
            Object.defineProperty(O, "keys", { get: () => () => ["changed"], configurable: true });
            console.log(typeof O.keys);
            console.log(O.keys({ x: 1 }).join(","));
            """, "function\nchanged\n"];
        yield return ["receivers", """
            const O: any = Object, alternate: any = {};
            let count = 0;
            const getter = function(this: any) { "use strict"; count++; return this; };
            Object.defineProperty(O, "custom", { get: getter, configurable: true });
            console.log(O.custom === O, O["custom"] === O, Reflect.get(O, "custom") === O, count);
            console.log(Reflect.get(O, "custom", alternate) === alternate,
                Reflect.get(O, "custom", undefined) === undefined,
                Reflect.get(O, "custom", null) === null, count);
            console.log(Object.getOwnPropertyDescriptor(O, "custom")!.get === getter);
            console.log(Reflect.get(O, "prototype") === O.prototype,
                Reflect.get(O, "keys") === O.keys);
            """, "true true true 3\ntrue true true 6\ntrue\ntrue true\n"];
        yield return ["missing-values", """
            const O: any = Object;
            Object.defineProperty(O, "keys", { value: null, configurable: true });
            console.log(O.keys === null, O["keys"] === null, Reflect.get(O, "keys") === null);
            Object.defineProperty(O, "keys", { value: undefined, configurable: true });
            console.log(O.keys === undefined, O["keys"] === undefined, Reflect.get(O, "keys") === undefined);
            """, "true true true\ntrue true true\n"];
        yield return ["throw-delete-restore", """
            const O: any = Object, original = O.keys, error: any = {};
            Object.defineProperty(O, "keys", { get: () => { throw error; }, configurable: true });
            try { O.keys; } catch (caught) { console.log(caught === error); }
            try { O["keys"]; } catch (caught) { console.log(caught === error); }
            try { Reflect.get(O, "keys"); } catch (caught) { console.log(caught === error); }
            delete O.keys;
            console.log(typeof O.keys);
            O.keys = original;
            console.log(O.keys({ x: 1 }).join(","), O.keys === original);
            """, "true\ntrue\ntrue\nundefined\nx true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void NamespaceDescriptorsUseTheirActualGetterReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedReflectGetWithMissingSymbolRetainsUndefinedResult()
    {
        const string source = "console.log(Reflect.get(Object, Symbol(\"missing\")) === undefined);";
        Assert.Equal("true\n", TestHarness.RunInterpreted(source));
    }
}
