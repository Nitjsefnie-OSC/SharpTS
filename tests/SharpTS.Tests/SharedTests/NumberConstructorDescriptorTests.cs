using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NumberConstructorDescriptorTests
{
    public const string IssueSource = """
        Object.defineProperty(Number, "custom", { value: 1 });
        console.log(Object.hasOwn(Number, "custom"));
        const constructor: any = Number;
        const descriptor = Object.getOwnPropertyDescriptor(constructor, "custom")!;
        console.log(constructor.custom, descriptor.value, descriptor.writable,
            descriptor.enumerable, descriptor.configurable);
        """;

    public const string DataSource = """
        const constructor: any = Number, key = "mutable";
        Object.defineProperty(constructor, key, { value: 3, writable: true, enumerable: true, configurable: true });
        console.log(constructor[key], Object.getOwnPropertyDescriptor(Number, key)!.value,
            Object.keys(constructor).includes(key));
        constructor[key] = 4;
        Object.defineProperty(Number, key, { value: 5 });
        const descriptor = Object.getOwnPropertyDescriptor(constructor, key)!;
        console.log(constructor[key], descriptor.writable, descriptor.enumerable, descriptor.configurable);
        Object.defineProperty(constructor, "nil", { value: null, configurable: true });
        Object.defineProperty(constructor, "missing", { value: undefined, configurable: true });
        Object.defineProperty(constructor, "empty", {});
        console.log(constructor.nil === null, constructor.missing === undefined, constructor.empty === undefined);
        console.log(Object.hasOwn(constructor, "nil"), Object.hasOwn(constructor, "missing"),
            Object.hasOwn(constructor, "empty"));
        console.log(Object.hasOwn(Object.getOwnPropertyDescriptor(constructor, "nil")!, "value"),
            Object.hasOwn(Object.getOwnPropertyDescriptor(constructor, "missing")!, "value"),
            Object.hasOwn(Object.getOwnPropertyDescriptor(constructor, "empty")!, "value"));
        console.log(delete constructor[key], Object.hasOwn(constructor, key),
            Object.getOwnPropertyDescriptor(Number, key) === undefined);
        """;

    public const string AccessorSource = """
        let reads = 0, writes = 0;
        const constructor: any = Number;
        function get(this: any) { reads++; return this.slot; }
        function set(this: any, value: any) { writes++; this.slot = value; }
        Object.defineProperty(constructor, "stored", { get, set, enumerable: true, configurable: true });
        const first = Object.getOwnPropertyDescriptor(constructor, "stored")!;
        const second = Object.getOwnPropertyDescriptor(Number, "stored")!;
        console.log(first.get === get, first.set === set, first.get === second.get, first.set === second.set,
            Object.hasOwn(first, "value"), Object.hasOwn(first, "writable"), reads, writes);
        constructor.slot = 4;
        console.log(constructor.stored, reads);
        constructor.stored = 9;
        console.log(constructor.slot, writes);
        const other: any = { slot: 12 };
        console.log(Reflect.get(constructor, "stored", other), first.get!.call(other));
        console.log(Reflect.set(constructor, "stored", 13, other), other.slot, constructor.slot, reads, writes);
        Object.defineProperty(constructor, "writeOnly", { set, configurable: true });
        Object.defineProperty(constructor, "readOnly", { get, configurable: true });
        console.log(constructor.writeOnly === undefined,
            Object.getOwnPropertyDescriptor(constructor, "writeOnly")!.get === undefined,
            Reflect.set(constructor, "readOnly", 1), reads, writes);
        """;

    public const string SetterSource = """
        let writes = 0;
        const constructor: any = Number;
        function set(this: any, value: any) { writes++; this.slot = value; }
        Object.defineProperty(constructor, "custom", { set, configurable: true });
        constructor.custom = 7;
        console.log(writes, constructor.slot === 7);
        Object.getOwnPropertyDescriptor(constructor, "custom")!.set!.call(constructor, 8);
        console.log(writes, constructor.slot === 8);
        constructor["custom"] = 9;
        function assignStrict() { "use strict"; constructor.custom = 10; }
        assignStrict();
        console.log(writes, constructor.slot === 10,
            Object.getOwnPropertyDescriptor(constructor, "custom")!.set === set);
        """;

    public const string InvariantSource = """
        const constructor: any = Number;
        Object.defineProperty(constructor, "fixed", { value: 7 });
        constructor.fixed = 8;
        console.log(constructor.fixed, Reflect.set(constructor, "fixed", 9));
        function strictWrite() { "use strict"; constructor.fixed = 8; }
        try { strictWrite(); } catch (error) { console.log(error instanceof TypeError); }
        console.log(Reflect.defineProperty(constructor, "fixed", { value: 7 }),
            Reflect.defineProperty(constructor, "fixed", { value: 9 }),
            Reflect.defineProperty(constructor, "fixed", { configurable: true }),
            Reflect.deleteProperty(constructor, "fixed"));
        try { Object.defineProperty(constructor, "fixed", { get() { return 7; } }); }
        catch (error) { console.log(error instanceof TypeError); }
        Object.defineProperties(constructor, {
            first: { value: 1, writable: true, configurable: true },
            second: { value: 2, enumerable: true, configurable: true }
        });
        console.log(constructor.first, constructor.second, Reflect.set(constructor, "first", 3),
            constructor.first, Object.keys(constructor).includes("second"));
        console.log(Reflect.deleteProperty(constructor, "first"), Reflect.deleteProperty(constructor, "second"),
            Object.hasOwn(constructor, "first"), Object.hasOwn(constructor, "second"));
        """;

    public const string IntrinsicSource = """
        const constructor: any = Number, prototype = Number.prototype;
        const keys = ["EPSILON", "MAX_VALUE", "MIN_VALUE", "MAX_SAFE_INTEGER", "MIN_SAFE_INTEGER",
            "POSITIVE_INFINITY", "NEGATIVE_INFINITY", "NaN"];
        let preserved = 0;
        for (const key of keys) {
            const descriptor = Object.getOwnPropertyDescriptor(constructor, key)!;
            if (!descriptor.writable && !descriptor.enumerable && !descriptor.configurable
                && Reflect.defineProperty(constructor, key, { value: descriptor.value })
                && !Reflect.defineProperty(constructor, key, { value: 0 })
                && !Reflect.deleteProperty(constructor, key)) preserved++;
        }
        console.log(preserved, Number.isNaN(Number.NaN));
        console.log(Reflect.defineProperty(constructor, "prototype", { value: prototype }),
            Reflect.defineProperty(constructor, "prototype", { value: {} }),
            Reflect.deleteProperty(constructor, "prototype"), constructor.prototype === prototype);
        const parse = Number.parseInt;
        console.log(parse === parseInt, Object.getOwnPropertyDescriptor(constructor, "parseInt")!.value === parse);
        Object.defineProperty(constructor, "parseInt", { enumerable: true });
        const method = Object.getOwnPropertyDescriptor(constructor, "parseInt")!;
        console.log(method.value === parse, method.writable, method.enumerable, method.configurable,
            constructor.parseInt("12"));
        console.log(delete constructor.parseInt, Object.hasOwn(constructor, "parseInt"));
        Object.defineProperty(constructor, "parseInt", { value: (value: any) => 99, configurable: true });
        console.log(constructor.parseInt("12"), parseInt === parse, parseInt("12"));
        const nameDescriptor = Object.getOwnPropertyDescriptor(constructor, "name")!;
        const lengthDescriptor = Object.getOwnPropertyDescriptor(constructor, "length")!;
        console.log(nameDescriptor.value, nameDescriptor.writable, nameDescriptor.enumerable, nameDescriptor.configurable,
            lengthDescriptor.value, lengthDescriptor.writable, lengthDescriptor.enumerable, lengthDescriptor.configurable);
        Object.defineProperty(constructor, "name", { value: "Numeric" });
        Object.defineProperty(constructor, "length", { value: 2 });
        console.log(constructor.name, constructor.length,
            Object.getOwnPropertyDescriptor(constructor, "name")!.configurable,
            Object.getOwnPropertyDescriptor(constructor, "length")!.configurable);
        """;

    public const string SymbolSource = """
        const constructor: any = Number, data = Symbol("data"), accessor = Symbol("accessor");
        Object.defineProperty(constructor, data, { value: 3, writable: true, enumerable: true, configurable: true });
        constructor[data] = 4;
        console.log(constructor[data], Object.getOwnPropertyDescriptor(constructor, data)!.value,
            Object.getOwnPropertySymbols(constructor).includes(data), Reflect.ownKeys(constructor).includes(data));
        let reads = 0, writes = 0;
        function get(this: any) { reads++; return this.slot; }
        function set(this: any, value: any) { writes++; this.slot = value; }
        Object.defineProperty(constructor, accessor, { get, set, configurable: true });
        const descriptor = Object.getOwnPropertyDescriptor(constructor, accessor)!;
        console.log(descriptor.get === get, descriptor.set === set, reads, writes);
        constructor.slot = 7;
        console.log(constructor[accessor]);
        constructor[accessor] = 8;
        const other: any = { slot: 9 };
        console.log(Reflect.get(constructor, accessor, other), Reflect.set(constructor, accessor, 10, other),
            other.slot, constructor.slot, reads, writes);
        console.log(Reflect.deleteProperty(constructor, data), constructor[data] === undefined,
            Object.getOwnPropertyDescriptor(constructor, data) === undefined);
        Object.defineProperty(constructor, accessor, { configurable: false });
        console.log(Reflect.deleteProperty(constructor, accessor),
            Reflect.defineProperty(constructor, accessor, { value: 1 }),
            Object.getOwnPropertyDescriptor(constructor, accessor)!.get === get);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true\n1 1 false false false\n"];
        yield return ["data", DataSource, "3 3 true\n5 true true true\ntrue true true\ntrue true true\ntrue true true\ntrue false true\n"];
        yield return ["accessors", AccessorSource, "true true true true false false 0 0\n4 1\n9 1\n12 12\ntrue 13 9 3 2\ntrue true false 3 2\n"];
        yield return ["setter-issue", SetterSource, "1 true\n2 true\n4 true true\n"];
        yield return ["invariants", InvariantSource, "7 false\ntrue\ntrue false false false\ntrue\n1 2 true 3 true\ntrue true false false\n"];
        yield return ["intrinsics", IntrinsicSource, "8 true\ntrue false false true\ntrue true\ntrue true true true 12\ntrue false\n99 true 12\nNumber false false true 1 false false true\nNumeric 2 true true\n"];
        yield return ["symbols", SymbolSource, "4 4 true true\ntrue true 0 0\n7\n9 true 10 8 2 2\ntrue true true\nfalse false true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        // Compiled native constructors retain separate getterless accessor (#2087),
        // Symbol and metadata limitations; #2024 repairs Number's interpreted descriptors.
        => Cases().SelectMany(row => ((string)row[0] is "issue" or "data" or "invariants" or "setter-issue"
                ? new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
                : new[] { ExecutionMode.Interpreted })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void NumberConstructorDefinitionsHonorDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedNumberDescriptorMutationsStayWithinTheirRealm()
    {
        const string first = """
            const constructor: any = Number;
            Object.defineProperty(constructor, "realmOnly", { value: 7 });
            Object.defineProperty(constructor, "name", { value: "Changed" });
            console.log(constructor.realmOnly, constructor.name);
            """;
        const string second = """
            const constructor: any = Number;
            console.log(Object.hasOwn(constructor, "realmOnly"), constructor.realmOnly === undefined,
                constructor.name, Number.parseInt === parseInt);
            """;
        Assert.Equal("7 Changed\n", TestHarness.RunInterpreted(first));
        Assert.Equal("false true Number true\n", TestHarness.RunInterpreted(second));
    }
}
