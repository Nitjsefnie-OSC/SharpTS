using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArrayCompoundAssignmentReadOrderTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", """
            const values = [1];
            const result = values[0] += (() => { values[0] = 10; return 2; })();
            console.log(result, values[0]);
            """, "3 3\n"];
        yield return ["numeric-and-string-operations", """
            const values = [8], strings: any[] = ["left"];
            function rhs(value: number) { values[0] = 100; return value; }
            values[0] = 8; console.log(values[0] -= rhs(3), values[0]);
            values[0] = 8; console.log(values[0] *= rhs(3), values[0]);
            values[0] = 8; console.log(values[0] /= rhs(2), values[0]);
            values[0] = 8; console.log(values[0] %= rhs(3), values[0]);
            values[0] = 3; console.log(values[0] <<= rhs(2), values[0]);
            values[0] = 6; console.log(values[0] &= rhs(3), values[0]);
            values[0] = 6; console.log(values[0] ^= rhs(3), values[0]);
            values[0] = 4; console.log(values[0] |= rhs(3), values[0]);
            const textRhs: any = () => { strings[0] = "changed"; return "right"; };
            console.log(strings[0] += textRhs(), strings[0]);
            """, "5 5\n24 24\n4 4\n2 2\n12 12\n2 2\n5 5\n7 7\nleftright leftright\n"];
        yield return ["typed-array-saved-value-and-narrowing", """
            const integer = new Int32Array([8]), byte = new Uint8Array([250]), floating = new Float64Array([1.5]);
            console.log(integer[0] += (() => { integer[0] = 100; return 2; })(), integer[0]);
            console.log(byte[0] += (() => { byte[0] = 100; return 10; })(), byte[0]);
            console.log(floating[0] *= (() => { floating[0] = 100; return 2; })(), floating[0]);
            """, "10 10\n260 4\n3 3\n"];
        yield return ["receiver-key-once-and-abrupt-reference-evaluation", """
            const values = [1], other = [20], events: string[] = [];
            let selected = values, selectedIndex = 0, receivers = 0, keys = 0, rights = 0;
            function receiver() { events.push("receiver"); receivers++; return selected; }
            function key() { events.push("key"); keys++; return selectedIndex; }
            function rhs() {
                events.push("rhs"); rights++;
                values[0] = 100; selected = other; selectedIndex = 1;
                return 2;
            }
            console.log(receiver()[key()] += rhs(), values[0], other[0], receivers, keys, rights, events.join(","));
            const token = {};
            events.length = 0;
            function badReceiver(): number[] { events.push("receiver"); throw token; }
            try { badReceiver()[key()] += rhs(); }
            catch (error) { console.log(error === token, events.join(","), rights); }
            events.length = 0;
            function badKey(): number { events.push("key"); throw token; }
            try { values[badKey()] += rhs(); }
            catch (error) { console.log(error === token, events.join(","), rights); }
            """, "3 3 20 1 1 1 receiver,key,rhs\ntrue receiver 1\ntrue key 1\n"];
        yield return ["old-object-reference-coerces-after-rhs", """
            const events: string[] = [];
            const original: any = {
                value: 1,
                valueOf() { events.push("left:" + this.value); return this.value; }
            };
            const right: any = { valueOf() { events.push("right"); return 3; } };
            const values: any[] = [original];
            const result = values[0] += (() => {
                events.push("rhs");
                values[0] = 100;
                original.value = 4;
                return right;
            })();
            console.log(result, values[0], original.value, events.join(","));
            """, "7 7 4 rhs,left:4,right\n"];
        yield return ["awaited-rhs-preserves-array-reference-and-read", """
            async function run() {
                const values = [1], other = [20], events: string[] = [];
                let selected = values;
                function receiver() { events.push("receiver"); return selected; }
                function key() { events.push("key"); return 0; }
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                async function change() { return receiver()[key()] += await pending; }
                const task = change();
                console.log(events.join(","), values[0]);
                values[0] = 100; selected = other; release(2);
                console.log(await task, values[0], other[0], events.join(","));
                const typed = new Int32Array([4]);
                let releaseTyped: (value: number) => void = () => {};
                const typedPending = new Promise<number>(resolve => { releaseTyped = resolve; });
                async function changeTyped() { return typed[0] *= await typedPending; }
                const typedTask = changeTyped();
                typed[0] = 100; releaseTyped(3);
                console.log(await typedTask, typed[0]);
            }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "receiver,key 1\n3 3 20 receiver,key\n12 12\n"];
        yield return ["logical-array-assignment-short-circuit-controls", """
            const values: any[] = [0, 1, null];
            let calls = 0;
            function rhs(value: number) { calls++; return value; }
            console.log(values[0] ||= rhs(4), values[0], calls);
            console.log(values[1] ||= rhs(9), values[1], calls);
            console.log(values[1] &&= rhs(5), values[1], calls);
            console.log(values[0] ??= rhs(6), values[0], calls);
            console.log(values[2] ??= rhs(7), values[2], calls);
            """, "4 4 1\n1 1 1\n5 5 2\n4 4 2\n7 7 3\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void IndexedCompoundAssignmentsSaveTheReadBeforeTheRightOperand(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
