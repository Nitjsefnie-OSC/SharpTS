using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AccessorCompoundAssignmentTests
{
    public const string IssueSource = """
        const holder: any = {
            get value() { console.log("get"); return 4; },
            set value(v: any) { console.log("set", v); }
        };
        console.log(holder.value += 2);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "get\nset 6\n6\n"];
        yield return ["operators-and-results", """
            const state = { value: 0, gets: 0, sets: 0 };
            const holder: any = {
                get value() { state.gets++; return state.value; },
                set value(next: number) { state.sets++; state.value = next; }
            };
            function record(result: number) { return [result, state.value].join(":"); }
            const values: string[] = [];
            state.value = 8; values.push(record(holder.value += 2));
            state.value = 8; values.push(record(holder.value -= 3));
            state.value = 8; values.push(record(holder.value *= 3));
            state.value = 8; values.push(record(holder.value /= 2));
            state.value = 8; values.push(record(holder.value %= 3));
            state.value = 3; values.push(record(holder.value <<= 2));
            state.value = -16; values.push(record(holder.value >>= 2));
            state.value = -16; values.push(record(holder.value >>>= 2));
            state.value = 6; values.push(record(holder.value &= 3));
            state.value = 6; values.push(record(holder.value ^= 3));
            state.value = 4; values.push(record(holder.value |= 3));
            console.log(values.join(","), state.gets, state.sets);
            """, "10:10,5:5,24:24,4:4,2:2,12:12,-4:-4,1073741820:1073741820,2:2,5:5,7:7 11 11\n"];
        yield return ["receiver-read-and-rhs-once-in-order", """
            const state: any = { value: 4, order: "", picks: 0, gets: 0, rights: 0, sets: 0 };
            const holder: any = {
                get value() { state.order += "g"; state.gets++; return state.value; },
                set value(next: number) { state.order += "s"; state.sets++; state.value = next; }
            };
            const other: any = { value: 20 };
            let selected: any = holder;
            function receiver(): any { state.order += "r"; state.picks++; return selected; }
            function rhs() {
                state.order += "h"; state.rights++;
                state.value = 100; selected = other;
                return 2;
            }
            const result = receiver().value += rhs();
            console.log(result, state.value, other.value, state.picks, state.gets, state.rights, state.sets, state.order);
            """, "6 6 20 1 1 1 1 rghs\n"];
        yield return ["rhs-can-replace-the-write-descriptor", """
            const state = { order: "", last: 0 };
            const holder: any = {
                get value() { state.order += "g"; return 7; },
                set value(next: number) { state.order += "old"; }
            };
            const result = holder.value += (() => {
                state.order += "r";
                // Avoid the independent compiled defineProperty getter-read bug (#2303).
                delete holder.value;
                Object.defineProperty(holder, "value", {
                    set(next: number) { state.order += "s"; state.last = next; },
                    configurable: true
                });
                return 3;
            })();
            console.log(result, state.last, state.order);
            """, "10 10 grs\n"];
        yield return ["inherited-accessor-uses-the-original-receiver", """
            const state: any = { gets: 0, sets: 0, same: true, expected: null };
            const prototype: any = {
                stored: 30,
                get value() { state.gets++; state.same = state.same && this === state.expected; return this.stored; },
                set value(next: number) { state.sets++; state.same = state.same && this === state.expected; this.stored = next; }
            };
            const holder: any = Object.create(prototype);
            holder.stored = 4; state.expected = holder;
            console.log(holder.value += 2, holder.stored, prototype.stored, state.gets, state.sets, state.same,
                Object.hasOwn(holder, "value"));
            """, "6 6 30 1 1 true false\n"];
        yield return ["setterless-accessors-in-strict-and-sloppy-mode", """
            const state = { gets: 0, rights: 0 };
            const getterOnly: any = { get value() { state.gets++; return 4; } };
            function rhs() { state.rights++; return 2; }
            console.log(getterOnly.value += rhs(), state.gets, state.rights);
            function strictGetterOnly() { "use strict"; return getterOnly.value += rhs(); }
            try { strictGetterOnly(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError, state.gets, state.rights); }
            console.log(getterOnly.value, state.gets);
            """, "6 1 1\ntrue 2 2\n4 3\n"];
        yield return ["abrupt-completions", """
            const state: any = { order: "", stage: "", last: 0 };
            const marker: any = {};
            const holder: any = {
                get value() {
                    state.order += "g";
                    if (state.stage === "get") throw marker;
                    return 4;
                },
                set value(next: number) {
                    state.order += "s"; state.last = next;
                    if (state.stage === "set") throw marker;
                }
            };
            function rhs(): any {
                state.order += "r";
                if (state.stage === "rhs") throw marker;
                if (state.stage === "coerce") return { valueOf() { state.order += "c"; throw marker; } };
                return 2;
            }
            for (const stage of ["get", "rhs", "coerce", "set"]) {
                state.stage = stage; state.order = ""; state.last = 0;
                try { holder.value += rhs(); console.log(false); }
                catch (error) { console.log(error === marker, state.order, state.last); }
            }
            """, "true g 0\ntrue gr 0\ntrue grc 0\ntrue grs 6\n"];
        yield return ["coercion-occurs-after-rhs-and-before-setter", """
            const state: any = { order: "", last: 0 };
            const left: any = { valueOf() { state.order += "l"; return 4; } };
            const right: any = { valueOf() { state.order += "q"; return 3; } };
            const holder: any = {
                get value() { state.order += "g"; return left; },
                set value(next: any) { state.order += "s"; state.last = next; }
            };
            console.log(holder.value += (() => { state.order += "r"; return right; })(), state.last, state.order);
            """, "7 7 grlqs\n"];
        yield return ["await-retains-the-receiver-and-read", """
            const state = { value: 4, order: "", picks: 0 };
            const holder: any = {
                get value() { state.order += "g"; return state.value; },
                set value(next: number) { state.order += "s"; state.value = next; }
            };
            const other: any = { value: 20 };
            let selected: any = holder;
            function receiver(): any { state.order += "r"; state.picks++; return selected; }
            async function run() {
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                async function change() { return receiver().value += await pending; }
                const task = change();
                console.log(state.order, state.picks);
                state.value = 100; selected = other; release(2);
                console.log(await task, state.value, other.value, state.picks, state.order);
            }
            run();
            """, "rg 1\n6 6 20 1 rgs\n"];
        yield return ["increment-and-logical-assignment-controls", """
            const state: any = { value: 1, order: "" };
            const holder: any = {
                get value() { state.order += "g"; return state.value; },
                set value(next: any) { state.order += "s"; state.value = next; }
            };
            function rhs(next: number) { state.order += "r"; return next; }
            console.log(holder.value++, state.value, state.order);
            state.order = ""; console.log(++holder.value, state.value, state.order);
            state.order = ""; console.log(holder.value ||= rhs(9), state.value, state.order);
            state.order = ""; console.log(holder.value &&= rhs(7), state.value, state.order);
            state.value = null; state.order = "";
            console.log(holder.value ??= rhs(8), state.value, state.order);
            const callable = function() { return 1; };
            const data: any = { value: callable };
            state.order = "";
            console.log((data.value ||= rhs(9)) === callable, data.value === callable, state.order.length);
            """, "1 2 gs\n3 3 gs\n3 3 g\n7 7 grs\n8 8 grs\ntrue true 0\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AccessorCompoundAssignmentsUseNormalPropertyReadsAndWrites(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void GetterCanReplaceItsDescriptorBeforeTheCompoundWrite()
    {
        // Compiled defineProperty reads the old getter (#2303), recursively invoking
        // this self-replacing getter. Keep that independent defect out of this regression.
        const string source = """
            const state = { order: "" };
            const holder: any = {
                get value() {
                    state.order += "g";
                    Object.defineProperty(holder, "value", { value: 100, writable: true, configurable: true });
                    return 4;
                },
                set value(next: number) { state.order += "old"; }
            };
            console.log(holder.value += 2, holder.value, state.order);
            """;

        Assert.Equal("6 6 g\n", TestHarness.Run(source, ExecutionMode.Interpreted));
    }

    [Fact]
    public void GetterlessAccessorCompoundReadProducesNaNBeforeCallingItsSetter()
    {
        // Compiled getterless object-literal reads do not return undefined (#2515).
        const string source = """
            const state: any = { last: 0, sets: 0 };
            const holder: any = { set value(next: number) { state.sets++; state.last = next; } };
            function strictWrite() { "use strict"; return holder.value += 1; }
            console.log(Number.isNaN(holder.value += 1), Number.isNaN(state.last), state.sets);
            console.log(Number.isNaN(strictWrite()), Number.isNaN(state.last), state.sets);
            """;

        Assert.Equal("true true 1\ntrue true 2\n", TestHarness.Run(source, ExecutionMode.Interpreted));
    }
}
