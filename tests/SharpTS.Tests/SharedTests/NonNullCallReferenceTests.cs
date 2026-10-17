using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NonNullCallReferenceTests
{
    // Preserve the exact issue source, including its terminal CRLF.
    public const string OriginalSource = """
        'use strict';
        const object: any = { method() { return this; } };
        console.log((object.method!)() === object);
        console.log((object.method)() === object);
        """ + "\r\n";

    public const string WrapperSource = """
        'use strict';
        const object: any = { method() { return this; } };
        console.log(object.method!() === object, (object.method!)() === object,
            object["method"]!() === object, (object["method"]!)() === object);
        console.log(((object.method as any)!)() === object, ((object["method"]!) as any)() === object,
            ((object.method!) satisfies Function)() === object);
        console.log((object.method!)?.() === object, (object["method"]!)?.() === object);
        console.log((object.method)() === object, (object["method"])() === object,
            (object.method as any)() === object, (object.method satisfies Function)() === object);
        function discard() {}
        console.log((discard(), object.method!)() === undefined,
            (true ? object.method! : object.method!)() === undefined);
        const missing: any = { method: undefined };
        console.log((missing.method!)?.() === undefined, (missing["method"]!)?.() === undefined);
        """ + "\n";

    public const string OrderSource = """
        'use strict';
        const state = { order: "", bases: 0, keys: 0, reads: 0 };
        const callable: any = function(this: any, value: any) {
            state.order += "c"; return this === object && value === 7;
        };
        const object: any = { get method() { state.reads++; state.order += "g"; return callable; } };
        function receiver() { state.bases++; state.order += "b"; return object; }
        function key() { state.keys++; state.order += "k"; return "method"; }
        function argument() { state.order += "a"; return 7; }
        console.log((receiver()[key()]!)(argument()), state.order, state.bases, state.keys, state.reads);
        state.order = "";
        console.log((receiver().method!)(argument()), state.order, state.bases, state.keys, state.reads);
        """ + "\n";

    // Swap the getter's source directly to avoid the unrelated defineProperty getter-read bug #2303.
    public const string PendingSource = """
        'use strict';
        const state = { order: "", bases: 0, keys: 0, reads: 0 };
        const callable: any = function(this: any, value: any) {
            state.order += "c"; return this === object && value === 7;
        };
        let selected = callable;
        const object: any = { get method() { state.reads++; state.order += "g"; return selected; } };
        function receiver() { state.bases++; state.order += "b"; return object; }
        function key() { state.keys++; state.order += "k"; return "method"; }
        let release: any;
        const gate = new Promise<number>(resolve => { release = resolve; });
        async function run() {
            const result = (receiver()[key()]!)(await gate);
            console.log(result, state.order, state.bases, state.keys, state.reads);
        }
        const work = run();
        console.log("paused", state.order);
        selected = () => false;
        release(7);
        work.then(() => console.log("done"), error => console.log("unexpected", error));
        """ + "\n";

    public const string GeneratorSource = """
        'use strict';
        const object: any = { method() { return this; } };
        function* run(): Generator<any, void, any> {
            const first = (object.method!)(yield "pause");
            console.log(first === object);
            yield (object["method"]!)() === object;
        }
        const iterator = run();
        console.log(iterator.next().value);
        console.log(iterator.next(7).value);
        console.log(iterator.next().done);
        """ + "\n";

    // A contextual generator signature preserves protocol types without the #2255 declaration bug.
    public const string AsyncFamiliesSource = """
        'use strict';
        const owner: any = { method(value: number) { return this === owner && value === 7; } };
        const arrow = async (): Promise<void> => {
            console.log((((owner["method"]! as any) satisfies Function))(await Promise.resolve(7)));
        };
        const values: () => AsyncGenerator<string, boolean, number> = async function* () {
            yield "pause";
            return (owner.method!)(await Promise.resolve(7));
        };
        async function run(): Promise<void> {
            await arrow();
            const iterator = values();
            const first = await iterator.next();
            console.log(first.value, first.done);
            const second = await iterator.next(7);
            console.log(second.value, second.done);
        }
        run().then(() => console.log("done"), error => console.log("unexpected", error));
        """ + "\n";

    public const string AbruptSource = """
        'use strict';
        const events: string[] = [];
        const marker = {};
        function base(): any { events.push("base"); throw marker; }
        function key(): string { events.push("key"); return "method"; }
        function argument(): number { events.push("arg"); return 7; }
        try { (base()[key()]!)(argument()); }
        catch (error) { console.log(error === marker, events.join(",")); }
        events.length = 0;
        const getterOwner: any = { get method() { events.push("get"); throw marker; } };
        try { (getterOwner.method satisfies Function)(argument()); }
        catch (error) { console.log(error === marker, events.join(",")); }
        events.length = 0;
        const receiver: any = {};
        const owner: any = { method() { return this; } };
        const extracted = owner.method!;
        console.log(extracted() === undefined, extracted.bind(receiver)() === receiver,
            extracted.call(receiver) === receiver, extracted.apply(receiver, []) === receiver);
        """ + "\n";

    // Evaluate the computed key before rejecting a null nonoptional receiver.
    public const string NullIndexOrderSource = """
        'use strict';
        let effects = 0;
        const holder: any = null;
        try { (holder[(++effects, "method")]!)(); }
        catch (error) { console.log(error instanceof TypeError, effects); }
        """ + "\n";

    // Grouping ends the optional chain before this ordinary invocation.
    public const string GroupedOptionalOrderSource = """
        'use strict';
        let effects = 0;
        const holder: any = null;
        try { (holder?.method!)((++effects)); }
        catch (error) { console.log(error instanceof TypeError, effects); }
        """ + "\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-nonnull-member-reference", OriginalSource, "true\ntrue\n"];
        yield return ["wrappers-successful-optional-and-detached-values", WrapperSource, "true true true true\ntrue true true\ntrue true\ntrue true true true\ntrue true\ntrue true\n"];
        yield return ["receiver-key-getter-argument-order", OrderSource, "true bkgac 1 1 1\ntrue bgac 2 1 2\n"];
        yield return ["pending-async-callee-and-receiver-snapshot", PendingSource, "paused bkg\ntrue bkgc 1 1 1\ndone\n"];
        yield return ["generator-argument-suspension", GeneratorSource, "pause\ntrue\ntrue\ntrue\n"];
        yield return ["async-arrow-and-async-generator-references", AsyncFamiliesSource, "true\npause false\ntrue true\ndone\n"];
        yield return ["abrupt-reference-order-and-explicit-binding", AbruptSource, "true base\ntrue get\ntrue true true true\n"];
        yield return ["nonnull-index-call-null-key-order", NullIndexOrderSource, "true 1\n"];
        yield return ["grouped-optional-nonnull-call", GroupedOptionalOrderSource, "true 1\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void TypeOnlyCallWrappersPreserveReferencesAndArgumentOrder(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
