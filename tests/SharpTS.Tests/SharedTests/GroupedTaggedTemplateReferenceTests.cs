using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GroupedTaggedTemplateReferenceTests
{
    public const string OriginalSource = """
        'use strict';
        const object: any = { value: 7, tag(strings: any) { return this.value; } };
        console.log((object.tag)`text`);
        """ + "\n";

    public const string WrapperSource = """
        'use strict';
        const owner: any = { value: 7, tag(strings: any) { return this.value; } };
        console.log(owner.tag`direct`);
        console.log(owner["tag"]`direct`);
        console.log((owner["tag"])`grouped`);
        console.log((((owner.tag)))`nested`);
        console.log((owner.tag as any)`asserted`);
        console.log((owner["tag"] as any)`asserted`);
        console.log((owner.tag!)`nonnull`);
        console.log((owner.tag satisfies any)`satisfies`);
        console.log((((owner["tag"]! as any) satisfies any))`combined`);
        """ + "\n";

    public const string SelectedCallableSource = """
        'use strict';
        const events: string[] = [];
        const object: any = { value: 7 };
        Object.defineProperty(object, 'tag', { configurable: true, get() {
            events.push('get');
            return function(this: any, strings: any, value: any) {
                events.push('call:' + this.value + ':' + value); return this.value;
            };
        } });
        function base() { events.push('base'); return object; }
        function key() { events.push('key'); return 'tag'; }
        function substitute() {
            events.push('sub'); Object.defineProperty(object, 'tag', { value: () => 99 }); return 'X';
        }
        try { console.log((base()[key()])`text${substitute()}`); }
        catch (error) { console.log(error instanceof TypeError); }
        console.log(events.join(','));
        """ + "\n";

    public const string DetachedSource = """
        'use strict';
        const owner: any = { tag(strings: any) { return this === undefined; } };
        const detached = owner.tag;
        console.log((0, owner.tag)`comma`, detached`variable`, (true ? owner.tag : detached)`conditional`);
        console.log((String.raw)`a\nb`);
        """ + "\n";

    public const string AbruptSource = """
        const events: string[] = [];
        const marker = {};
        function base(): any { events.push("base"); throw marker; }
        function key(): string { events.push("key"); return "tag"; }
        function substitution(): number { events.push("sub"); return 1; }
        try { (base()[key()])`x${substitution()}`; }
        catch (error) { console.log(error === marker, events.join(",")); }
        events.length = 0;
        const getterOwner: any = { get tag() { events.push("get"); throw marker; } };
        try { (getterOwner.tag as any)`x${substitution()}`; }
        catch (error) { console.log(error === marker, events.join(",")); }
        events.length = 0;
        const tagOwner: any = { get tag() {
            events.push("get");
            return function(strings: any, value: any) { events.push("call"); return value; };
        } };
        function throwingSubstitution(): never { events.push("sub"); throw marker; }
        try { (tagOwner.tag!)`x${throwingSubstitution()}`; }
        catch (error) { console.log(error === marker, events.join(",")); }
        """ + "\n";

    public const string PendingAsyncSource = """
        'use strict';
        const events: string[] = [];
        const owner: any = { value: 7 };
        Object.defineProperty(owner, "tag", { configurable: true, get() {
            events.push("get");
            return function(this: any, strings: any, value: string) {
                events.push("call:" + this.value + ":" + value);
                return this.value + ":" + value;
            };
        } });
        let release: (value: string) => void = () => {};
        const gate = new Promise<string>(resolve => { release = resolve; });
        function base(): any { events.push("base"); return owner; }
        function key(): string { events.push("key"); return "tag"; }
        function substitution(): Promise<string> { events.push("sub"); return gate; }
        async function run(): Promise<void> {
            console.log(owner.tag`direct:${"S"}`);
            console.log((((base()[key()]! as any) satisfies any))`pending:${await substitution()}`);
            console.log(events.join(","));
        }
        const task = run();
        console.log("paused", events.join(","));
        Object.defineProperty(owner, "tag", { value: () => "replacement", configurable: true });
        release("X");
        task.then(() => console.log("done"), error => console.log("error", error));
        """ + "\n";

    public const string GeneratorYieldSource = """
        'use strict';
        const owner: any = { value: 7, tag(strings: any, value: string) { return this.value + ":" + value; } };
        function* run(): Generator<string, string, string> {
            return (((owner["tag"]! as any) satisfies any))`value:${yield "pause"}`;
        }
        const iterator = run();
        const first = iterator.next();
        console.log(first.value, first.done);
        owner.tag = () => "replacement";
        const second = iterator.next("X");
        console.log(second.value, second.done);
        """ + "\n";

    public const string AsyncFamiliesSource = """
        'use strict';
        const owner: any = { value: 9, tag(strings: any, value: string) { return this.value + ":" + value; } };
        const arrow = async (): Promise<void> => {
            console.log((owner.tag!)`arrow:${await Promise.resolve("A")}`);
        };
        async function* values(): AsyncGenerator<string, string, string> {
            const value = yield "pause";
            return (((owner["tag"]! as any) satisfies any))`generator:${await Promise.resolve(value)}`;
        }
        async function run(): Promise<void> {
            await arrow();
            const iterator = values();
            const first = await iterator.next();
            console.log(first.value, first.done);
            const second = await iterator.next("B");
            console.log(second.value, second.done);
        }
        run().then(() => console.log("done"), error => console.log("error", error));
        """ + "\n";

    // Keep the exact #2426 source, including its lack of a terminal newline.
    public const string SuspensionTryOriginalSource = """
        function tag(strings: any, value: any) { return value; } async function run() { try { console.log(tag`x${await Promise.resolve(1)}`); } catch (error) { console.log("caught"); } } run();
        """;

    // Separate catch-only tries require detection of each tag/substitution suspension.
    // Contextual generator expressions retain their protocol types while avoiding #2255.
    public const string SuspensionTryFamiliesSource = """
        type Tag = (strings: TemplateStringsArray, value: string) => string;
        function tag(strings: TemplateStringsArray, value: string): string { return strings[0] + value; }
        const marker = {};
        const syncValues: () => Generator<string | Tag, string, string | Tag> = function* () {
            try {
                console.log(((yield tag) as Tag)`tag:${"S"}`);
                throw marker;
            } catch (error) {
                console.log("sync-tag-caught", error === marker);
            }
            try {
                console.log(tag`sub:${(yield "sync-value") as string}`);
                throw marker;
            } catch (error) {
                console.log("sync-sub-caught", error === marker);
            }
            return "sync-done";
        };
        const arrow = async (): Promise<void> => {
            try {
                console.log((await Promise.resolve(tag))`tag:${"A"}`);
                throw marker;
            } catch (error) {
                console.log("arrow-tag-caught", error === marker);
            }
            try {
                console.log(tag`sub:${await Promise.resolve("B")}`);
                throw marker;
            } catch (error) {
                console.log("arrow-sub-caught", error === marker);
            }
        };
        const asyncValues: () => AsyncGenerator<string, string, string> = async function* () {
            try {
                console.log((await Promise.resolve(tag))`tag:${"G"}`);
                throw marker;
            } catch (error) {
                console.log("async-tag-caught", error === marker);
            }
            try {
                console.log(tag`sub:${yield "async-value"}`);
                throw marker;
            } catch (error) {
                console.log("async-yield-caught", error === marker);
            }
            try {
                console.log(tag`await:${await Promise.resolve("H")}`);
                throw marker;
            } catch (error) {
                console.log("async-await-caught", error === marker);
            }
            return "async-done";
        };
        async function run(): Promise<void> {
            try {
                console.log((await Promise.resolve(tag))`ordinary:${"O"}`);
                throw marker;
            } catch (error) {
                console.log("ordinary-tag-caught", error === marker);
            }
            const sync = syncValues();
            const first = sync.next();
            console.log(first.value === tag, first.done);
            const second = sync.next(tag);
            console.log(second.value, second.done);
            const third = sync.next("V");
            console.log(third.value, third.done);
            await arrow();
            const asynchronous = asyncValues();
            const start = await asynchronous.next();
            console.log(start.value, start.done);
            const finish = await asynchronous.next("Y");
            console.log(finish.value, finish.done);
        }
        run().then(() => console.log("done"), error => console.log("error", error));
        """ + "\n";

    // #2455: a null member read fails before substitutions; grouped receiver references remain intact.
    public const string ReceiverReadOrderSource = """
        'use strict';
        const holder: any = { inner: { value: 7, tag(strings: any) { return this.value; } } };
        console.log((holder?.inner).tag`dot`);
        console.log((holder?.inner)["tag"]`index`);
        console.log((holder?.inner)!.tag`nonnull`);
        const events: string[] = [];
        const nullOwner: any = null;
        try { nullOwner.tag`dot:${events.push("dot-sub")}`; }
        catch (error) { console.log(error instanceof TypeError); }
        try { nullOwner[(events.push("key"), "tag")]`index:${events.push("index-sub")}`; }
        catch (error) { console.log(error instanceof TypeError); }
        console.log(events.join(","));
        """ + "\n";

    // A grouped String.raw tag reads the live property value rather than the direct intrinsic path.
    public const string GroupedStringRawLiveSource = """
        const descriptor = Object.getOwnPropertyDescriptor(String, "raw")!;
        try {
            Object.defineProperty(String, "raw", { configurable: true, writable: true, value: () => "replacement" });
            console.log((String.raw)`text`);
        } finally {
            Object.defineProperty(String, "raw", descriptor);
        }
        console.log((String.raw)`a\nb`);
        """ + "\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-grouped-member", OriginalSource, "7\n"];
        yield return ["transparent-member-wrappers", WrapperSource, "7\n7\n7\n7\n7\n7\n7\n7\n7\n"];
        yield return ["selected-callable-and-effect-order", SelectedCallableSource, "7\nbase,key,get,sub,call:7:X\n"];
        yield return ["detached-values-and-string-raw", DetachedSource, "true true true\na\\nb\n"];
        yield return ["abrupt-reference-and-substitution-order", AbruptSource, "true base\ntrue get\ntrue get,sub\n"];
        yield return ["pending-async-selected-reference", PendingAsyncSource, "7:S\npaused get,call:7:S,base,key,get,sub\n7:X\nget,call:7:S,base,key,get,sub,call:7:X\ndone\n"];
        yield return ["generator-yield-selected-reference", GeneratorYieldSource, "pause false\n7:X true\n"];
        yield return ["async-arrow-and-async-generator", AsyncFamiliesSource, "9:A\npause false\n9:B true\ndone\n"];
        yield return ["issue-2426-awaited-tagged-template-in-try", SuspensionTryOriginalSource, "1\n"];
        yield return ["tag-and-substitution-suspension-in-try", SuspensionTryFamiliesSource, "ordinary:O\nordinary-tag-caught true\ntrue false\ntag:S\nsync-tag-caught true\nsync-value false\nsub:V\nsync-sub-caught true\nsync-done true\ntag:A\narrow-tag-caught true\nsub:B\narrow-sub-caught true\ntag:G\nasync-tag-caught true\nasync-value false\nsub:Y\nasync-yield-caught true\nawait:H\nasync-await-caught true\nasync-done true\ndone\n"];
        yield return ["grouped-receiver-and-null-read-order", ReceiverReadOrderSource, "7\n7\n7\ntrue\ntrue\nkey\n"];
        yield return ["grouped-string-raw-live-policy", GroupedStringRawLiveSource, "replacement\na\\nb\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void GroupedTaggedTemplatesPreserveReferencesAndSuspension(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
