using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class CollectionConsumerIteratorTests
{
    // Object cells keep these checks independent of nested-generator scalar
    // capture writeback (#2269); each check call also owns its closure scope.
    public const string InterleavingSource = """
        function check(consumer: number) {
            const original = {};
            const state: any = { events: "" };
            const entry: any = {};
            Object.defineProperty(entry, "0", {
                get() { state.events += "consume;"; throw original; }
            });
            function* values() {
                try { state.events += "yield;"; yield entry; state.events += "resumed;"; throw "later"; }
                finally { state.events += "close;"; throw "cleanup"; }
            }
            try {
                if (consumer === 0) Array.from(values(), () => { state.events += "consume;"; throw original; });
                else if (consumer === 1) Object.groupBy(values(), () => { state.events += "consume;"; throw original; });
                else Object.fromEntries(values());
            } catch (error) { console.log(consumer, error === original, state.events); }
        }
        for (let consumer = 0; consumer < 3; consumer++) check(consumer);
        """;

    public const string InterleavingExpected = "0 true yield;consume;close;\n"
        + "1 true yield;consume;close;\n2 true yield;consume;close;\n";

    public const string CloseFailuresSource = """
        function check(consumer: number, kind: number) {
            const original = {}, cleanup = {};
            let factoryGets = 0, factoryCalls = 0, nextGets = 0, nextCalls = 0;
            let returnGets = 0, returnCalls = 0, sourceReturns = 0, receivers = true;
            const entry: any = {};
            Object.defineProperty(entry, "0", { get() { throw original; } });
            const iterator: any = {};
            Object.defineProperty(iterator, "next", {
                get() {
                    nextGets++; receivers = receivers && this === iterator;
                    return function() {
                        nextCalls++; receivers = receivers && this === iterator;
                        return nextCalls === 1 ? { done: false, value: entry } : { done: true };
                    };
                }
            });
            Object.defineProperty(iterator, "return", {
                get() {
                    returnGets++; receivers = receivers && this === iterator;
                    if (kind === 0) throw cleanup;
                    if (kind === 2) return 1;
                    return function() {
                        returnCalls++; receivers = receivers && this === iterator;
                        if (kind === 1) throw cleanup;
                        return kind === 3 ? 1 : {};
                    };
                }
            });
            const source: any = [];
            source.return = () => { sourceReturns++; throw cleanup; };
            Object.defineProperty(source, Symbol.iterator, {
                get() {
                    factoryGets++; receivers = receivers && this === source;
                    return function() {
                        factoryCalls++; receivers = receivers && this === source;
                        return iterator;
                    };
                }
            });
            let caught: any;
            try {
                if (consumer === 0) Array.from(source, () => { throw original; });
                else if (consumer === 1) Object.groupBy(source, () => { throw original; });
                else Object.fromEntries(source);
            } catch (error) { caught = error; }
            console.log(consumer, kind, caught === original, factoryGets, factoryCalls,
                nextGets, nextCalls, returnGets, returnCalls, sourceReturns, receivers);
        }
        for (let consumer = 0; consumer < 3; consumer++)
            for (let kind = 0; kind < 5; kind++) check(consumer, kind);
        """;

    public static string CloseFailuresExpected => string.Concat(
        Enumerable.Range(0, 3).SelectMany(consumer => Enumerable.Range(0, 5)
            .Select(kind => $"{consumer} {kind} true 1 1 1 1 1 {(kind is 0 or 2 ? 0 : 1)} 0 true\n")));

    // Current AddEntriesFromIterable propagates IteratorStepValue failures without
    // closing. Node 22 still closes Object.fromEntries for these protocol failures.
    // https://tc39.es/ecma262/2026/multipage/keyed-collections.html#sec-add-entries-from-iterable
    public const string ProtocolFailuresSource = """
        function check(consumer: number, kind: number) {
            const original = {};
            let returnGets = 0, returnCalls = 0, steps = 0;
            const iterator: any = {};
            Object.defineProperty(iterator, "next", {
                get() {
                    if (kind === 2) throw original;
                    if (kind === 7) return 1;
                    return function() {
                        if (kind === 3) throw original;
                        if (kind === 6) return 1;
                        if (steps++ > 0) return { done: true };
                        return {
                            get done() { if (kind === 4) throw original; return false; },
                            get value() { if (kind === 5) throw original; return ["key", 1]; }
                        };
                    };
                }
            });
            Object.defineProperty(iterator, "return", {
                get() { returnGets++; return function() { returnCalls++; return {}; }; }
            });
            const source: any = [];
            Object.defineProperty(source, Symbol.iterator, {
                get() {
                    if (kind === 0) throw original;
                    return function() { if (kind === 1) throw original; return iterator; };
                }
            });
            let correctError = false;
            try {
                if (consumer === 0) Array.from(source);
                else if (consumer === 1) Object.groupBy(source, () => "group");
                else Object.fromEntries(source);
            } catch (error) {
                correctError = kind >= 6 ? error instanceof TypeError : error === original;
            }
            console.log(consumer, kind, correctError, returnGets, returnCalls);
        }
        for (let consumer = 0; consumer < 3; consumer++)
            for (let kind = 0; kind < 8; kind++) check(consumer, kind);
        """;

    public static string ProtocolFailuresExpected => string.Concat(
        Enumerable.Range(0, 3).SelectMany(consumer => Enumerable.Range(0, 8)
            .Select(kind => $"{consumer} {kind} true 0 0\n")));

    public const string NormalCompletionSource = """
        function check(consumer: number) {
            let factoryGets = 0, factoryCalls = 0, nextGets = 0, steps = 0, processed = 0;
            let returnGets = 0, terminalValues = 0, receivers = true;
            const iterator: any = {};
            const observe = () => {
                processed++;
                Object.defineProperty(iterator, "next", {
                    value: () => { throw "replacement next"; }, configurable: true
                });
            };
            Object.defineProperty(iterator, "next", {
                configurable: true,
                get() {
                    nextGets++; receivers = receivers && this === iterator;
                    return function() {
                        receivers = receivers && this === iterator;
                        const index = steps++;
                        if (index === 2) return {
                            done: true, get value() { terminalValues++; throw "terminal value"; }
                        };
                        if (index > 2) throw "too many steps";
                        const entry: any = { 1: index };
                        Object.defineProperty(entry, "0", {
                            get() { observe(); return "key" + index; }
                        });
                        return { done: false, value: entry };
                    };
                }
            });
            Object.defineProperty(iterator, "return", {
                get() { returnGets++; throw "normal close"; }
            });
            const source: any = [];
            Object.defineProperty(source, Symbol.iterator, {
                get() {
                    factoryGets++; receivers = receivers && this === source;
                    return function() {
                        factoryCalls++; receivers = receivers && this === source;
                        return iterator;
                    };
                }
            });
            if (consumer === 0) console.log(Array.from(source, () => { observe(); return 1; }).join(","));
            else if (consumer === 1) console.log(Object.groupBy(source, () => { observe(); return "group"; }).group.length);
            else console.log(Object.fromEntries(source).key1);
            console.log(consumer, processed, factoryGets, factoryCalls, nextGets, steps,
                returnGets, terminalValues, receivers);
        }
        for (let consumer = 0; consumer < 3; consumer++) check(consumer);
        """;

    public const string NormalCompletionExpected = "1,1\n0 2 1 1 1 3 0 0 true\n"
        + "2\n1 2 1 1 1 3 0 0 true\n1\n2 2 1 1 1 3 0 0 true\n";

    public const string EntryFailuresSource = """
        function check(kind: number) {
            const original = {};
            const state: any = { events: "" };
            const entry: any[] = [];
            const prototype: any = {};
            Object.defineProperty(kind === 1 ? entry : prototype, "0", {
                get() {
                    state.events += "key:" + (this === entry) + ";";
                    if (kind === 1) throw original;
                    return "key";
                }
            });
            Object.defineProperty(prototype, "1", {
                get() { state.events += "value:" + (this === entry) + ";"; throw original; }
            });
            Object.setPrototypeOf(entry, prototype);
            function* values() {
                try { state.events += "yield;"; yield kind === 0 ? 1 : entry; state.events += "resumed;"; }
                finally { state.events += "close;"; throw "cleanup"; }
            }
            try { Object.fromEntries(values()); console.log("returned"); }
            catch (error) {
                console.log(kind, kind === 0 ? error instanceof TypeError : error === original, state.events);
            }
        }
        for (let kind = 0; kind < 3; kind++) check(kind);
        """;

    public const string EntryFailuresExpected = "0 true yield;close;\n"
        + "1 true yield;key:true;close;\n2 true yield;key:true;value:true;close;\n";

    public const string CompatibilitySource = """
        const context: any = { scale: 3 }, values = [1, 2];
        let receivers = true, indexes = "";
        const mapped = Array.from(values, function(value: number, index: number) {
            receivers = receivers && this === context; indexes += index;
            if (index === 0) values.push(3);
            return value * this.scale;
        }, context);
        console.log(mapped.join(","), receivers, indexes);
        const groupedValues = [1, 2];
        let groupReceivers = true, groupIndexes = "";
        const grouped: any = Object.groupBy(groupedValues, function(value: number, index: number) {
            "use strict";
            groupReceivers = groupReceivers && this === undefined; groupIndexes += index;
            if (index === 0) groupedValues.push(3);
            return value % 2;
        });
        console.log(Object.getPrototypeOf(grouped) === null, grouped[1].join(","),
            grouped[0].join(","), groupReceivers, groupIndexes);
        class Values { *[Symbol.iterator]() { yield 4; yield 5; } }
        console.log(Array.from(new Values()).join(","));
        console.log(Array.from(new Set([2, 1])).join(","));
        console.log(Array.from(new Uint8Array([3, 4])).join(","));
        console.log(Array.from("😀x").join("|"));
        console.log(JSON.stringify(Array.from(new Map([["x", 1]]))));
        const arrayLike: any = { 0: "a", 2: "c", length: 3 };
        console.log(JSON.stringify(Array.from(arrayLike)));
        """;

    public const string CompatibilityExpected = "3,6,9 true 012\ntrue 1,3 2 true 012\n"
        + "4,5\n2,1\n3,4\n😀|x\n[[\"x\",1]]\n[\"a\",null,\"c\"]\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["interleaving", InterleavingSource, InterleavingExpected];
        yield return ["close-failures", CloseFailuresSource, CloseFailuresExpected];
        yield return ["protocol-failures", ProtocolFailuresSource, ProtocolFailuresExpected];
        yield return ["normal-completion", NormalCompletionSource, NormalCompletionExpected];
        yield return ["entry-failures", EntryFailuresSource, EntryFailuresExpected];
        yield return ["compatibility", CompatibilitySource, CompatibilityExpected];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ConsumersPreserveIteratorProtocolBoundaries(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    // These native carriers' descriptor support is a compiled-runtime boundary.
    [Theory, CompiledOnlyData]
    public void NativeCarriersDoNotRestoreAnExplicitlyShadowedIterator(ExecutionMode mode)
    {
        const string source = """
            for (let carrier = 0; carrier < 3; carrier++) {
                for (let descriptor = 0; descriptor < 4; descriptor++) {
                    const source: any = carrier === 0 ? new Set([1, 2])
                        : carrier === 1 ? new Map([["key", 1]]) : new Uint8Array([1, 2]);
                    const state: any = { reads: 0 };
                    if (descriptor === 0) Object.defineProperty(source, Symbol.iterator, { value: undefined });
                    else if (descriptor === 1) Object.defineProperty(source, Symbol.iterator, { set(value: any) {} });
                    else if (descriptor === 2) Object.defineProperty(source, Symbol.iterator, {
                        get() { state.reads++; return undefined; }
                    });
                    else Object.setPrototypeOf(source, { [Symbol.iterator]: undefined });
                    let groupRejected = false, entriesRejected = false;
                    try { Object.groupBy(source, () => 0); }
                    catch (error) { groupRejected = error instanceof TypeError; }
                    try { Object.fromEntries(source); }
                    catch (error) { entriesRejected = error instanceof TypeError; }
                    console.log(carrier, descriptor, groupRejected, entriesRejected, state.reads);
                }
            }
            """;
        var expected = string.Concat(Enumerable.Range(0, 3).SelectMany(carrier =>
            Enumerable.Range(0, 4).Select(descriptor =>
                $"{carrier} {descriptor} true true {(descriptor == 2 ? 2 : 0)}\n")));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, CompiledOnlyData]
    public void ArrayFromNativeCarriersWithNullishIteratorUseIndexedArrayLikeGets(ExecutionMode mode)
    {
        const string source = """
            for (let carrier = 0; carrier < 3; carrier++) {
                for (let descriptor = 0; descriptor < 4; descriptor++) {
                    const source: any = carrier === 0 ? new Set([1, 2])
                        : carrier === 1 ? new Map([["key", 1]]) : new Uint8Array([1, 2]);
                    const state: any = { reads: 0 };
                    if (descriptor === 0) Object.defineProperty(source, Symbol.iterator, { value: undefined });
                    else if (descriptor === 1) Object.defineProperty(source, Symbol.iterator, { set(value: any) {} });
                    else if (descriptor === 2) Object.defineProperty(source, Symbol.iterator, {
                        get() { state.reads++; return undefined; }
                    });
                    else Object.defineProperty(source, Symbol.iterator, { value: null });
                    console.log(carrier, descriptor, Array.from(source).join(","), state.reads);
                }
            }
            """;
        var expected = string.Concat(Enumerable.Range(0, 3).SelectMany(carrier =>
            Enumerable.Range(0, 4).Select(descriptor =>
                $"{carrier} {descriptor} {(carrier == 2 ? "1,2" : "")} {(descriptor == 2 ? 1 : 0)}\n")));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ArrayLikeGetsInterleaveWithMapperAndRetainCapturedLength(ExecutionMode mode)
    {
        const string source = """
            const source: any = { 0: 1, 1: 2, length: 2 };
            const context = {};
            let correctReceiver = true;
            const result = Array.from(source, function(this: any, value: any, index: number) {
                correctReceiver = correctReceiver && this === context;
                if (index === 0) { source[1] = 9; source.length = 0; }
                return value;
            }, context);
            console.log(result.join(","), correctReceiver);
            """;
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal("1,9 true\n", output);
        }
        else
        {
            Assert.Equal("1,9 true\n", TestHarness.Run(source, mode));
        }
    }

    public static IEnumerable<object[]> ArrayLikeMappingOrderCases()
    {
        yield return ["live-inherited-index-and-captured-length", """
            const state = { events: "", mapperThis: true, getterThis: true };
            const context = {}, prototype: any = {};
            Object.defineProperty(prototype, "1", {
                get() { state.events += "old1;"; return -1; }, configurable: true
            });
            const source: any = Object.create(prototype);
            source.marker = 9;
            Object.defineProperty(source, "length", {
                get() { state.events += "length;"; return 2; }, configurable: true
            });
            Object.defineProperty(source, "0", {
                get() { state.events += "get0;"; return 1; }
            });
            const result = Array.from(source, function(this: any, value: any, index: number) {
                state.events += "map" + index + ";";
                state.mapperThis = state.mapperThis && this === context;
                if (index === 0) {
                    Object.defineProperty(source, "length", { value: 0, configurable: true });
                    Object.defineProperty(prototype, "1", {
                        get: function(this: any) {
                            state.events += "new1;";
                            state.getterThis = state.getterThis && this === source;
                            return this.marker;
                        }, configurable: true
                    });
                }
                return value;
            }, context);
            console.log(result.join(","), source.length, state.mapperThis, state.getterThis);
            console.log(state.events);
            """, "1,9 0 true true\nlength;get0;map0;new1;map1;\n"];

        yield return ["mapper-throw-before-later-index", """
            const original = {}, poison = {}, cleanup = {};
            const state = { events: "", maps: 0, laterGets: 0, returnGets: 0, returnCalls: 0 };
            const source: any = {
                get length() { state.events += "length;"; return 2; },
                get 0() { state.events += "get0;"; return 1; },
                get 1() { state.events += "get1;"; state.laterGets++; throw poison; },
                get return() {
                    state.returnGets++;
                    return function() { state.returnCalls++; throw cleanup; };
                }
            };
            try {
                Array.from(source, function(value: any, index: number) {
                    state.events += "map" + index + ";";
                    state.maps++;
                    throw original;
                });
            } catch (error) {
                console.log(error === original, error === poison, state.maps, state.laterGets,
                    state.returnGets, state.returnCalls, state.events);
            }
            """, "true false 1 0 0 0 length;get0;map0;\n"];

        yield return ["indexed-throw-before-next-map-without-close", """
            const original = {}, poison = {}, cleanup = {};
            const state = { events: "", maps: 0, laterGets: 0, returnGets: 0, returnCalls: 0 };
            const source: any = {
                get length() { state.events += "length;"; return 3; },
                get 0() { state.events += "get0;"; return 1; },
                get 1() { state.events += "get1;"; throw original; },
                get 2() { state.events += "get2;"; state.laterGets++; throw poison; },
                get return() {
                    state.returnGets++;
                    return function() { state.returnCalls++; throw cleanup; };
                }
            };
            try {
                Array.from(source, function(value: any, index: number) {
                    state.events += "map" + index + ";";
                    state.maps++;
                    return value;
                });
            } catch (error) {
                console.log(error === original, error === poison, state.maps, state.laterGets,
                    state.returnGets, state.returnCalls, state.events);
            }
            """, "true false 1 0 0 0 length;get0;map0;get1;\n"];
    }

    public static IEnumerable<object[]> ArrayLikeMappingOrderModeCases() =>
        ArrayLikeMappingOrderCases().SelectMany(row =>
            new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
                .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ArrayLikeMappingOrderModeCases))]
    public void ArrayLikeMappingInterleavesReadsAndStopsAtAbruptCompletion(
        string name, string source, string expected, ExecutionMode mode)
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

    [Theory, CompiledOnlyData]
    public void ArrayLikeLengthRetainsPrecisionAndRejectsInvalidResultLengthBeforeIndexGets(ExecutionMode mode)
    {
        const string source = """
            let reads = 0;
            const original = {};
            const large: any = { length: 2147483648, get 0() { reads++; throw original; } };
            try { Array.from(large); }
            catch (error) { console.log(error === original, reads); }
            const invalid: any = { length: 4294967296, get 0() { reads++; throw original; } };
            try { Array.from(invalid); }
            catch (error) { console.log(error instanceof RangeError, reads); }
            """;
        Assert.Equal("true 1\ntrue 1\n", TestHarness.Run(source, mode));
    }
}
