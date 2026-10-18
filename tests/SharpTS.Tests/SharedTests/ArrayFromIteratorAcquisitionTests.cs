using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArrayFromIteratorAcquisitionTests
{
    public const string ClassSource = """
        const marker = {}, state: any = { reads: 0, gets: 0, calls: 0, valid: true };
        function* values(owner: any) { yield owner; }
        const factory: any = function(this: any) {
            state.calls++; state.valid = state.valid && this === source;
            return values(this);
        };
        class Base {
            static get [Symbol.iterator]() {
                state.gets++; state.reads++; state.valid = state.valid && this === source;
                if (state.reads > 1) throw marker;
                return factory;
            }
        }
        class Derived extends Base {}
        const source: any = Derived;
        const plain = Array.from(source);
        state.reads = 0;
        const explicit = Array.from(source, undefined as any);
        state.reads = 0;
        const mapped = Array.from(source, (value: any) => value);
        console.log(plain[0] === source, explicit[0] === source, mapped[0] === source,
            state.gets, state.calls, state.valid, plain !== explicit && explicit !== mapped);
        """;

    public const string MappedObjectSource = """
        const item = {}, context = { offset: 10 };
        const state: any = { gets: 0, calls: 0, nextGets: 0, events: "", valid: true }, source: any = {};
        Object.defineProperty(source, Symbol.iterator, {
            get() {
                state.gets++; state.valid = state.valid && this === source;
                return function(this: any) {
                    state.calls++; state.valid = state.valid && this === source;
                    const progress = { index: 0 }, iterator: any = {};
                    Object.defineProperty(iterator, "next", {
                        get() {
                            state.nextGets++; state.valid = state.valid && this === iterator;
                            return function(this: any) {
                                state.valid = state.valid && this === iterator;
                                const index = progress.index++;
                                state.events += "n" + index;
                                return index < 2 ? { value: item, done: false } : { done: true };
                            };
                        }
                    });
                    return iterator;
                };
            }
        });
        const result = Array.from(source, function(this: any, value: any, index: number) {
            state.valid = state.valid && this === context && value === item;
            state.events += "m" + index;
            return index + this.offset;
        }, context);
        console.log(result.join(","), state.events, state.gets, state.calls, state.nextGets, state.valid);
        const again = Array.from(source);
        console.log(again.length, again[0] === item, again !== result, state.gets, state.calls, state.nextGets);
        """;

    public const string ValidationSource = """
        const marker = {}, state = { gets: 0, calls: 0 }, source: any = {};
        Object.defineProperty(source, Symbol.iterator, { get() { state.gets++; throw marker; } });
        try { Array.from(source, null as any); }
        catch (error) { console.log(error instanceof TypeError, state.gets, state.calls); }
        try { Array.from(source); }
        catch (error) { console.log(error === marker, state.gets, state.calls); }
        const invalid: any = { get [Symbol.iterator]() { state.gets++; return 7; } };
        try { Array.from(invalid); }
        catch (error) { console.log(error instanceof TypeError, state.gets, state.calls); }
        const factoryThrows: any = {
            get [Symbol.iterator]() {
                state.gets++;
                return function() { state.calls++; throw marker; };
            }
        };
        try { Array.from(factoryThrows); }
        catch (error) { console.log(error === marker, state.gets, state.calls); }
        const arrayLike: any = { 0: "a", 1: "b", length: 2 };
        console.log(Array.from(arrayLike).join(","));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["array-like-length-getter", """
            const state = { reads: 0 }, input: any = {
                0: 'a', 1: 'b', get length() { state.reads++; return 2; }
            };
            console.log(Array.from(input).join(','), state.reads);
            """, "a,b 1\n"];
        yield return ["array-like-inherited-accessors", """
            const state = { events: '', valid: true };
            const proto: any = {
                get length() {
                    state.events += 'l'; state.valid = state.valid && this === input;
                    return { valueOf() { state.events += 'v'; return 3.9; } };
                },
                get 0() { state.events += '0'; state.valid = state.valid && this === input; return 'a'; }
            };
            const input: any = Object.create(proto);
            Object.defineProperty(input, '1', {
                get() { state.events += '1'; state.valid = state.valid && this === input; return 'b'; }
            });
            const result = Array.from(input);
            console.log(result.length, result.join(','), typeof result[2], state.events, state.valid);
            """, "3 a,b, undefined lv01 true\n"];
        yield return ["array-like-accessor-errors", """
            const marker = {}, state = { events: '' };
            const lengthThrows: any = { get length() { state.events += 'l'; throw marker; } };
            try { Array.from(lengthThrows); }
            catch (error) { console.log(error === marker, state.events); }
            const indexThrows: any = {
                get length() { state.events += 'l'; return 2; },
                get 0() { state.events += '0'; throw marker; },
                get 1() { state.events += '1'; return 1; }
            };
            try { Array.from(indexThrows); }
            catch (error) { console.log(error === marker, state.events); }
            """, "true l\ntrue ll0\n"];
        yield return ["nullish-data-iterator", """
            for (const method of [null, undefined]) {
                const input: any = { 0: 7, length: 1, [Symbol.iterator]: method };
                console.log(Array.from(input).join(','));
            }
            """, "7\n7\n"];
        yield return ["class-first-method", ClassSource, "true true true 3 3 true true\n"];
        yield return ["mapped-object", MappedObjectSource, "10,11 n0m0n1m1n2 1 1 1 true\n2 true true 2 2 2\n"];
        yield return ["validation-and-fallback", ValidationSource, "true 0 0\ntrue 1 0\ntrue 2 0\ntrue 3 1\na,b\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ArrayFromRetainsOneAcquisitionAndMapsBeforeAdvancing(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    // Compiled required iteration accepts ordinary undefined iterator slots (#2574).
    [Fact]
    public void RequiredConsumersRejectNullishRecordIteratorSlots()
    {
        const string source = """
            for (const method of [null, undefined]) {
                const input: any = { 0: 7, length: 1, [Symbol.iterator]: method };
                try { for (const value of input) {} }
                catch (error) { console.log(error instanceof TypeError); }
                try { const values = [...input]; }
                catch (error) { console.log(error instanceof TypeError); }
            }
            """;
        Assert.Equal("true\ntrue\ntrue\ntrue\n", TestHarness.Run(source, ExecutionMode.Interpreted));
    }
}
