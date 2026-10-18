using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NestedGeneratorLiveCaptureTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-scalar", """
            function probe() {
                let trace = "";
                function* values() { trace += "a"; yield trace; trace += "b"; yield trace; }
                const iterator = values();
                console.log(iterator.next().value, trace === "a");
                console.log(iterator.next().value, trace === "ab");
            }
            probe();
            """, "a true\nab true\n"];
        yield return ["issue-spread", """
            function probe() {
                let trace = "";
                function consume(...values: number[]) { return values.join(","); }
                function* args() { trace += "s"; yield 1; trace += "t"; yield 2; trace += "e"; }
                function tail() { trace += "z"; return 0; }
                console.log(consume(...args(), tail()), trace);
                trace = "";
                const token: any = {};
                function* broken() { trace += "b"; yield 1; trace += "t"; throw token; }
                try { consume(...broken(), tail()); }
                catch (error) { console.log(trace, error === token); }
            }
            probe();
            """, "1,2,0 stez\nbt true\n"];
        yield return ["owner-and-sibling", """
            function probe() {
                let value = 0;
                function* values() { value++; yield value; value += 10; yield value; }
                function read() { return value; }
                const iterator = values();
                value = 4;
                console.log(iterator.next().value, value, read());
                value = 20;
                console.log(iterator.next().value, value, read());
            }
            probe();
            """, "5 5 5\n30 30 30\n"];
        yield return ["abrupt-and-invocations", """
            function make() {
                let trace = "";
                const token: any = {};
                function* values() {
                    try { trace += "a"; yield trace; trace += "b"; yield trace; }
                    finally { trace += "f"; }
                }
                return { values, read: () => trace, token };
            }
            const returned = make();
            const first: any = returned.values();
            console.log(first.next().value, returned.read());
            console.log(first.return("stop").value, returned.read());
            const thrown = make();
            const second: any = thrown.values();
            console.log(second.next().value, thrown.read());
            try { second.throw(thrown.token); }
            catch (error) { console.log(error === thrown.token, thrown.read()); }
            console.log(returned.read(), thrown.read());
            """, "a a\nstop af\na a\ntrue af\naf af\n"];
        yield return ["loop-binding-identity", """
            function probe() {
                const records: any[] = [];
                for (let index = 0; index < 2; index++) {
                    let value = index * 10;
                    function* values() { value++; yield value; value++; yield value; }
                    records.push({ iterator: values(), read: () => value });
                }
                console.log(records[0].iterator.next().value, records[1].iterator.next().value);
                console.log(records[0].read(), records[1].read());
                console.log(records[1].iterator.next().value, records[0].iterator.next().value);
                console.log(records[0].read(), records[1].read());
            }
            probe();
            """, "1 11\n1 11\n12 2\n2 12\n"];
        yield return ["retained-expression-control", """
            function probe() {
                let trace = "";
                {
                    const anchor = 1;
                    const values = function* () {
                        trace += "a"; yield trace;
                        trace += "b"; yield trace;
                        return anchor;
                    };
                    const iterator = values();
                    console.log(iterator.next().value, trace === "a");
                    console.log(iterator.next().value, trace === "ab");
                }
            }
            probe();
            """, "a true\nab true\n"];
        yield return ["hoist-identity-and-shared-iterators", """
            function make(seed: number) {
                let value = seed;
                const before = values;
                const first = values(), second = values();
                function* values() { value++; yield value; }
                console.log(before === values, values.name, values.length, typeof values.prototype);
                return { first, second, read: () => value };
            }
            const one = make(0), two = make(10);
            console.log(one.first.next().value, one.read(), one.second.next().value, one.read());
            console.log(two.first.next().value, two.read(), one.read());
            """, "true values 0 object\ntrue values 0 object\n1 1 2 2\n11 11 2\n"];
        yield return ["mutable-declaration-self-binding", """
            {
                let value = 0;
                function* values(): Generator<any> { value++; yield values; value++; yield value; }
                const original = values;
                const iterator = values();
                function* replacement(): Generator<any> { yield "replacement"; }
                // @ts-ignore: function declarations are mutable at runtime.
                values = replacement;
                console.log(iterator.next().value === replacement, value);
                console.log(iterator.next().value, value, original !== values);
            }
            """, "true 1\n2 2 true\n"];
        yield return ["read-only-async-generator-owners", """
            async function* outer(seed: number) {
                let value = seed;
                const values = async function* () { yield value; yield value; };
                const iterator = values();
                value++;
                yield (await iterator.next()).value;
                value += 10;
                yield (await iterator.next()).value;
            }
            async function run() {
                const first = outer(0), second = outer(10);
                console.log((await first.next()).value, (await second.next()).value);
                console.log((await second.next()).value, (await first.next()).value);
            }
            run();
            """, "1 11\n21 11\n"];
        yield return ["read-only-async-generator-shadows", """
            async function* outer(seed: number) {
                let value = seed;
                const outerRead = async function* () { yield value; };
                {
                    let value = seed + 100;
                    const innerRead = async function* () { yield value; };
                    value++;
                    yield (await innerRead().next()).value;
                }
                value++;
                yield (await outerRead().next()).value;
            }
            async function run() {
                const first = outer(0), second = outer(10);
                console.log((await first.next()).value, (await second.next()).value);
                console.log((await second.next()).value, (await first.next()).value);
            }
            run();
            """, "101 111\n11 1\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void NestedGeneratorsShareTheirOriginalCapturedBindings(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
