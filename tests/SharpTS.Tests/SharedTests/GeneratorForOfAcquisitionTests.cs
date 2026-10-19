using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GeneratorForOfAcquisitionTests
{
    public const string IssueSource = """
        class C {
            static *[Symbol.iterator]() {
                console.log("receiver", this === C);
                yield 42;
                yield 43;
            }
        }
        const iterable: any = C;
        function* run() { for (const value of iterable) yield value; }
        try {
            for (const value of run()) console.log(value);
            console.log("done");
        } catch (error: any) { console.log("caught", error.message); }
        """;

    public const string RetainedIteratorSource = """
        const state: any = { gets: 0, calls: 0, nextGets: 0, nextCalls: 0, valid: true };
        const iterator: any = { index: 0 };
        function next(this: any) {
            state.nextCalls++; state.valid = state.valid && this === iterator;
            this.index++;
            return this.index <= 2 ? { value: this.index, done: false } : { done: true };
        }
        Object.defineProperty(iterator, "next", {
            get() { state.nextGets++; return next; }, configurable: true
        });
        function factory(this: any) {
            state.calls++; state.valid = state.valid && this === Derived;
            return iterator;
        }
        class Base {
            static get [Symbol.iterator]() {
                state.gets++; state.valid = state.valid && this === Derived;
                return factory;
            }
        }
        class Derived extends Base {}
        const iterable: any = Derived;
        function* run() { for (const value of iterable) yield value; }
        const outer = run();
        console.log(state.gets, state.calls, state.nextGets, state.nextCalls);
        console.log(outer.next().value, state.gets, state.calls, state.nextGets, state.nextCalls);
        Object.defineProperty(iterator, "next", { value: () => { throw "replacement"; }, configurable: true });
        console.log(outer.next().value, state.gets, state.calls, state.nextGets, state.nextCalls);
        console.log(outer.next().done, state.gets, state.calls, state.nextGets, state.nextCalls, state.valid);
        """;

    public const string AcquisitionErrorsSource = """
        const marker = {}, state = { gets: 0, calls: 0 };
        function* consume(source: any) { for (const value of source) yield value; }
        class GetterThrows {
            static get [Symbol.iterator]() { state.gets++; throw marker; }
        }
        const getterSource: any = GetterThrows;
        try { consume(getterSource).next(); }
        catch (error) { console.log(error === marker, state.gets, state.calls); }
        function factory() { state.calls++; throw marker; }
        class FactoryThrows {
            static get [Symbol.iterator]() { state.gets++; return factory; }
        }
        const factorySource: any = FactoryThrows;
        try { consume(factorySource).next(); }
        catch (error) { console.log(error === marker, state.gets, state.calls); }
        class Noncallable {
            static get [Symbol.iterator]() { state.gets++; return 7; }
        }
        const invalidSource: any = Noncallable;
        try { consume(invalidSource).next(); }
        catch (error) { console.log(error instanceof TypeError, state.gets, state.calls); }
        """;

    public const string NativeCarriersSource = """
        function* values() {
            for (const value of [1, 2]) yield value;
            const set = new Set<number>([3, 4]);
            for (const value of set) yield value;
            const dynamic: any = new Set<number>([5]);
            for (const value of dynamic) yield value;
        }
        console.log(Array.from(values()).join(","));
        function* knownMap() {
            const map = new Map<number, string>([[1, "a"], [2, "b"]]);
            for (const entry of map) yield entry;
        }
        for (const entry of knownMap()) console.log(Array.isArray(entry), entry[0], entry[1]);
        function* dynamicMap() {
            const map: any = new Map<number, string>([[3, "c"]]);
            for (const entry of map) yield entry;
        }
        for (const entry of dynamicMap()) console.log(Array.isArray(entry), entry[0], entry[1]);
        function* nested() {
            for (const left of [1, 2]) for (const right of [3, 4]) yield left * 10 + right;
        }
        console.log(Array.from(nested()).join(","));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "receiver true\n42\n43\ndone\n"];
        yield return ["retained-iterator", RetainedIteratorSource,
            "0 0 0 0\n1 1 1 1 1\n2 1 1 1 2\ntrue 1 1 1 3 true\n"];
        yield return ["acquisition-errors", AcquisitionErrorsSource, "true 1 0\ntrue 2 1\ntrue 3 1\n"];
        yield return ["native-carriers", NativeCarriersSource,
            "1,2,3,4,5\ntrue 1 a\ntrue 2 b\ntrue 3 c\n13,14,23,24\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void YieldingForOfAcquiresAndRetainsTheGuestIterator(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
