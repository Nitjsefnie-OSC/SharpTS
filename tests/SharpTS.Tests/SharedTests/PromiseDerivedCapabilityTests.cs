using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PromiseDerivedCapabilityTests
{
    public const string IssueSource = """
        const replacement: any = { value: 43 };
        let installCapability = true;
        class Returned extends Promise<any> {
            constructor(executor: any) {
                if (installCapability) super(executor);
                else super(() => {});
                return replacement;
            }
        }
        class Source extends Promise<any> {
            static get [Symbol.species]() { return Returned; }
        }
        const source: any = new Source(resolve => resolve(1));
        try { console.log("valid", source.then() === replacement); }
        catch (error) { console.log("valid error", error instanceof TypeError); }
        console.log("static", Returned.resolve(1) === replacement);
        installCapability = false;
        try { source.then(); console.log("missing no error"); }
        catch (error) { console.log("missing", error instanceof TypeError); }
        """;

    public const string EntryPointsSource = """
        const state: any = { constructs: 0, species: 0, replacements: [] };
        class Returned extends Promise<any> {
            constructor(executor: any) {
                super(executor);
                state.constructs++;
                const replacement: any = { id: state.constructs };
                state.replacements.push(replacement);
                return replacement;
            }
        }
        class Source extends Promise<any> {
            static get [Symbol.species]() { state.species++; return Returned; }
        }
        const source = new Source(resolve => resolve(1));
        const direct: any = source.then();
        console.log(direct === state.replacements[0], state.constructs, state.species);
        const then: any = Promise.prototype.then;
        const borrowed: any = then.call(source);
        console.log(borrowed === state.replacements[1], state.constructs);
        // #2349: the built-in checker requires a callable catch/finally argument.
        const caught: any = source.catch(undefined as any);
        console.log(caught === state.replacements[2], state.constructs);
        const finalized: any = source.finally(undefined as any);
        console.log(finalized === state.replacements[3], state.constructs);
        const resolve: any = Promise.resolve;
        const staticResult: any = resolve.call(Returned, 1);
        console.log(staticResult === state.replacements[4], state.constructs);
        """;

    public const string ValidationOrderSource = """
        const token = {};
        const state: any = { mode: 0, constructs: 0, valid: false, order: [], calls: [] };
        let release: any, reject: any;
        class Returned extends Promise<any> {
            constructor(executor: any) {
                super(() => {});
                state.constructs++;
                state.order.push("construct");
                if (state.mode === 1) executor(1, 2);
                if (state.mode === 2) throw token;
                return ({} as any);
            }
        }
        class Source extends Promise<any> {
            static get [Symbol.species]() { return state.valid ? Promise : Returned; }
        }
        function handler(label: any): any {
            state.order.push("arg:" + label);
            return () => { state.calls.push(label); };
        }
        async function run() {
            const source = new Source(resolve => { release = resolve; });
            const rejected = new Source((resolve, rejectValue) => { reject = rejectValue; });
            try { source.then(handler("then")); }
            catch (error) { console.log("then", error instanceof TypeError); }
            const then: any = Promise.prototype.then;
            state.mode = 1;
            try { then.call(source, handler("borrowed")); }
            catch (error) { console.log("borrowed", error instanceof TypeError); }
            state.mode = 0;
            try { rejected.catch(handler("catch")); }
            catch (error) { console.log("catch", error instanceof TypeError); }
            state.mode = 1;
            try { source.finally(handler("finally")); }
            catch (error) { console.log("finally", error instanceof TypeError); }
            state.mode = 2;
            try { source.then(handler("throw")); }
            catch (error) { console.log("throw", error === token); }
            console.log("sync", state.constructs, state.calls.length, state.order.join(","));
            state.valid = true;
            rejected.catch(() => {});
            release(1);
            reject("reason");
            await new Promise<any>(resolve => { setTimeout(() => resolve(0), 0); });
            console.log("after", state.constructs, state.calls.length);
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string StaticCombinatorSource = """
        const state: any = { valid: true, constructs: 0, order: [], replacement: undefined, receiver: false };
        class Returned extends Promise<any> {
            constructor(executor: any) {
                state.order.push("construct");
                if (state.valid) super(executor);
                else super(() => {});
                state.constructs++;
                const replacement: any = { id: state.constructs };
                state.replacement = replacement;
                return replacement;
            }
            static get resolve(): any {
                state.order.push("resolve");
                state.receiver = this === Returned;
                return Promise.resolve;
            }
        }
        const iterable: any = {
            [Symbol.iterator]() {
                state.order.push("iterator");
                return { next() { state.order.push("next"); return { done: true }; } };
            }
        };
        const result: any = Returned.all(iterable);
        console.log(result === state.replacement, state.constructs, state.receiver, state.order.join(","));
        state.valid = false;
        state.order = [];
        try { Returned.all(iterable); console.log("no error"); }
        catch (error) { console.log(error instanceof TypeError, state.constructs, state.order.join(",")); }
        """;

    public const string SuspendedArgumentsSource = """
        const state: any = { order: [], receivers: 0, release: undefined, replacement: undefined };
        class Returned extends Promise<any> {
            constructor(executor: any) {
                super(executor);
                state.order.push("construct");
                const replacement: any = {};
                state.replacement = replacement;
                return replacement;
            }
        }
        class Source extends Promise<any> {
            static get [Symbol.species]() { state.order.push("species"); return Returned; }
        }
        const source: any = new Source(resolve => resolve(1));
        function receiver(): any { state.receivers++; state.order.push("receiver"); return source; }
        function observe(label: string, value: any): any { state.order.push(label); return value; }
        function gate(label: string): Promise<any> {
            state.order.push("wait:" + label);
            return new Promise<any>(resolve => { state.release = resolve; });
        }
        async function direct() {
            const result: any = receiver().then(
                ...([observe("first", undefined)] as [any]),
                await gate("then"), observe("surplus", 3));
            console.log("then", result === state.replacement, state.receivers, state.order.join(","));
        }
        async function inherited() {
            const result: any = Returned.resolve(...([await gate("static")] as [any]));
            console.log("static", result === state.replacement, state.order.join(","));
        }
        async function run() {
            const pending = direct();
            console.log("paused then", state.order.join(","));
            state.release(observe("release:then", undefined));
            await pending;
            state.order = [];
            const pendingStatic = inherited();
            console.log("paused static", state.order.join(","));
            state.release(observe("release:static", 7));
            await pendingStatic;
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "valid true\nstatic true\nmissing true\n"];
        yield return ["entry-points", EntryPointsSource, "true 1 1\ntrue 2\ntrue 3\ntrue 4\ntrue 5\n"];
        yield return ["validation-order", ValidationOrderSource,
            "then true\nborrowed true\ncatch true\nfinally true\nthrow true\n" +
            "sync 5 0 arg:then,construct,arg:borrowed,construct,arg:catch,construct,arg:finally,construct,arg:throw,construct\n" +
            "after 5 0\n"];
        yield return ["static-combinator", StaticCombinatorSource,
            "true 1 true construct,resolve,iterator,next\ntrue 2 construct\n"];
        yield return ["suspended-arguments", SuspendedArgumentsSource,
            "paused then receiver,first,wait:then\n" +
            "then true 1 receiver,first,wait:then,release:then,surplus,species,construct\n" +
            "paused static wait:static\nstatic true wait:static,release:static,construct\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DerivedPromiseFactoriesUseValidatedConstructorCapabilities(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
