using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed partial class AsyncReturnThenableTests
{
    public const string IssueSource = """
        async function answer(): Promise<any> {
            const value: any = { then(resolve: any) { resolve(47); } };
            return value;
        }
        async function run() { console.log(await answer()); }
        run();
        """;

    public const string CallableFormsSource = """
        function make(item: number): any { return { then(resolve: any) { resolve(item); } }; }
        async function declared(): Promise<any> { return make(1); }
        const block = async (): Promise<any> => { return make(2); };
        const expression = async (): Promise<any> => make(3);
        const ordinary = async function(): Promise<any> { return make(4); };
        class Declaration {
            async read(): Promise<any> { return make(5); }
            static async read(): Promise<any> { return make(6); }
            async #private(): Promise<any> { return make(7); }
            async privateValue(): Promise<any> { return this.#private(); }
        }
        const Expression = class Inner {
            async read(): Promise<any> { return make(8); }
            static async read(): Promise<any> { return make(9); }
        };
        async function run() {
            const declaration = new Declaration(), instance = new Expression();
            console.log(await declared(), await block(), await expression(), await ordinary(),
                await declaration.read(), await Declaration.read(), await declaration.privateValue(),
                await instance.read(), await Expression.read());
        }
        run();
        """;

    public const string CaptureAndPendingSource = """
        const state: any = { events: [], gets: 0, calls: 0, replacements: 0, valid: true }, marker = {};
        const source: any = {};
        const first: any = function(this: any, resolve: any, reject: any) {
            state.calls++; state.valid = state.valid && this === source; state.events.push("then");
            resolve({ then(fulfill: any) { setTimeout(() => fulfill(47), 1); } });
            reject(marker);
            throw marker;
        };
        Object.defineProperty(source, "then", {
            configurable: true,
            get() {
                state.gets++; state.valid = state.valid && this === source;
                state.events.push("get"); return first;
            }
        });
        async function answer(): Promise<any> { return source; }
        function start() {
            const label = "caller", pending = answer();
            state.events.push("after");
            Object.defineProperty(source, "then", {
                configurable: true, value: () => { state.replacements++; }
            });
            return pending.then(value => console.log(label, value, state.events.join(","),
                state.gets, state.calls, state.replacements, state.valid));
        }
        start();
        """;

    public const string RejectionAndFinallySource = """
        const marker = {}, other = {}, state = { gets: 0, before: 0, after: 0, unused: 0 };
        async function pass(value: any): Promise<any> { return value; }
        const finalValue: any = { then(resolve: any) { state.before++; resolve(1); } };
        const unusedValue: any = { then(resolve: any) { state.unused++; resolve(99); } };
        async function replaced(): Promise<any> {
            try { return finalValue; }
            finally {
                Object.defineProperty(finalValue, "then", {
                    value(resolve: any) { state.after++; resolve(2); }, configurable: true
                });
            }
        }
        async function finalThrow(): Promise<any> {
            try { return unusedValue; }
            finally { throw marker; }
        }
        async function run() {
            try { await pass({ then(resolve: any, reject: any) { reject(marker); resolve(99); throw other; } }); }
            catch (error) { console.log(error === marker); }
            try { await pass({ then() { throw marker; } }); }
            catch (error) { console.log(error === marker); }
            const getter: any = {};
            Object.defineProperty(getter, "then", { get() { state.gets++; throw marker; } });
            try { await pass(getter); }
            catch (error) { console.log(error === marker, state.gets); }
            console.log(await replaced(), state.before, state.after);
            try { await finalThrow(); }
            catch (error) { console.log(error === marker, state.unused); }
        }
        run();
        """;

    public const string PreservationSource = """
        const state = { writes: 0 }, getterless: any = {};
        Object.defineProperty(getterless, "then", { set(value: any) { state.writes++; } });
        async function pass(value: any): Promise<any> { return value; }
        async function run() {
            for (const value of [{}, { then: undefined }, { then: null }, { then: 7 }, getterless]) {
                console.log((await pass(value)) === value);
            }
            console.log(state.writes);
            console.log(await pass(7), typeof (await pass(null)), (await pass(null)) === null,
                typeof (await pass(undefined)));
        }
        run();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "47\n"];
        yield return ["callable-forms", CallableFormsSource, "1 2 3 4 5 6 7 8 9\n"];
        yield return ["capture-and-pending-first-wins", CaptureAndPendingSource,
            "caller 47 get,after,then 1 1 0 true\n"];
        yield return ["rejection-and-finally", RejectionAndFinallySource, "true\ntrue\ntrue 1\n2 0 1\ntrue 0\n"];
        foreach (var row in CompletionCases()) yield return row;
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AsyncReturnsAssimilateThenablesAtTheFinalCompletionBoundary(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    public const string PreservationExpected = "true\ntrue\ntrue\ntrue\ntrue\n0\n7 object true undefined\n";

    [Theory, ModeData]
    public void NoncallableThenPropertiesPreserveTheOriginalObjectAndPrimitiveValues(ExecutionMode mode)
    {
        Assert.Equal(PreservationExpected, TestHarness.Run(PreservationSource, mode));
    }
}
