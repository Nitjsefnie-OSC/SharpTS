using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NumericObjectSlotEqualityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            function read(value: number = 1) {
                console.log(value, value === 0, value == 0, value === null);
            }
            const f: any = read;
            f(null);
            """, "null false false true\n"];
        yield return ["two-annotated-object-slots-all-operators", """
            function compare(left: number = undefined as any, right: number = undefined as any) {
                console.log(left === right, left !== right, left == right, left != right,
                    right === left, right !== left, right == left, right != left);
            }
            function strict(left: number = undefined as any, right: number = undefined as any) {
                console.log(left === right, left !== right, right === left, right !== left);
            }
            const invoke: any = compare;
            invoke(null, 0);
            invoke(null, undefined);
            invoke(0, 0);
            const strictInvoke: any = strict;
            strictInvoke(0, "0");
            strictInvoke(false, 0);
            """, "false true false true false true false true\nfalse true true false false true true false\ntrue false true false true false true false\nfalse true false true\nfalse true false true\n"];
        yield return ["native-and-raw-both-orders", """
            function compare(native: number, raw: any) {
                console.log(native === raw, native !== raw, native == raw, native != raw,
                    raw === native, raw !== native, raw == native, raw != native);
            }
            function strict(native: number, raw: any) {
                console.log(native === raw, native !== raw, raw === native, raw !== native);
            }
            compare(0, null);
            compare(0, undefined);
            compare(0, 0);
            compare(0, new Number(0));
            const nan: number = 0 / 0;
            compare(nan, nan);
            compare(0, -0);
            strict(0, "0");
            strict(0, false);
            """, "false true false true false true false true\nfalse true false true false true false true\ntrue false true false true false true false\nfalse true true false false true true false\nfalse true false true false true false true\ntrue false true false true false true false\nfalse true false true\nfalse true false true\n"];
        yield return ["get-values-before-conversion-and-abrupt-right", """
            const state = { trace: "", conversions: 0 };
            const token: any = {};
            const primitive: any = {
                valueOf() { state.trace += "V"; state.conversions++; return 0; }
            };
            const values: any = {
                get left() { state.trace += "L"; return primitive; },
                get right() { state.trace += "R"; return 0; }
            };
            console.log((values.left as number) == (values.right as number), state.trace, state.conversions);
            state.trace = "";
            console.log((values.left as number) === (values.right as number), state.trace, state.conversions);
            state.trace = "";
            console.log((values.right as number) != (values.left as number), state.trace, state.conversions);
            state.trace = "";
            console.log((values.right as number) !== (values.left as number), state.trace, state.conversions);
            state.trace = "";
            const throwing: any = {
                get left() { state.trace += "L"; return primitive; },
                get right() { state.trace += "R"; throw token; }
            };
            try { (throwing.left as number) == (throwing.right as number); }
            catch (error) { console.log(error === token, state.trace, state.conversions); }
            """, "true LRV 1\nfalse LR 1\nfalse RLV 2\ntrue RL 2\ntrue LR 2\n"];
        yield return ["captured-defaults-through-arrow-and-function", """
            function make(value: number = 1) {
                const arrow = () => console.log(value, value === 0, value !== 0, value == 0, value != 0,
                    0 === value, 0 == value);
                const ordinary = function() { console.log(value, value === 0, value == 0, value === null); };
                return { arrow, ordinary };
            }
            const factory: any = make;
            const nullish: any = factory(null);
            nullish.arrow(); nullish.ordinary();
            const zero: any = factory(0);
            zero.arrow(); zero.ordinary();
            const omitted: any = factory(undefined);
            omitted.arrow(); omitted.ordinary();
            """, "null false true false true false false\nnull false false true\n0 true false true false true true\n0 true true false\n1 false true false true false false\n1 false false false\n"];
        yield return ["pending-async-and-arrow-save-left", """
            async function pending(state: any, wait: any) {
                const same = (state.value as number) === await wait;
                console.log("async", same, state.value);
            }
            const pendingArrow = async (state: any, wait: any) => {
                const same = (state.value as number) == await wait;
                console.log("arrow", same, state.value);
            };
            function gate(state: any): any {
                return new Promise<any>((resolve: any) => { state.release = resolve; });
            }
            const firstState: any = { value: null }, secondState: any = { value: 0 };
            const firstGate: any = gate(firstState), secondGate: any = gate(secondState);
            const first: any = pending(firstState, firstGate), second: any = pendingArrow(secondState, secondGate);
            firstState.value = 7; secondState.value = 8;
            firstState.release(null); secondState.release(0);
            first.then(() => second).then(() => {}, (error: any) => console.log("rejected", error));
            """, "async true 7\narrow true 8\n"];
        yield return ["generator-save-left-across-yield", """
            function* compare(state: any): any {
                const strict = (state.value as number) === ((yield "strict") as any);
                console.log("generator", strict, state.value);
                state.value = 0;
                const loose = (state.value as number) == ((yield "loose") as any);
                console.log("generator-loose", loose, state.value);
            }
            const state: any = { value: null };
            const iterator: any = compare(state);
            console.log(iterator.next().value);
            state.value = 7;
            console.log(iterator.next(null).value);
            state.value = 8;
            console.log(iterator.next(0).done);
            """, "strict\ngenerator true 7\nloose\ngenerator-loose true 8\ntrue\n"];
        yield return ["async-generator-save-left-across-await-and-yield", """
            async function* compare(state: any, wait: any): any {
                const awaited = (state.value as number) === await wait;
                console.log("async-generator-await", awaited, state.value);
                state.value = null;
                const strict = (state.value as number) === ((yield "strict") as any);
                console.log("async-generator", strict, state.value);
                state.value = 0;
                const loose = (state.value as number) == ((yield "loose") as any);
                console.log("async-generator-loose", loose, state.value);
            }
            function gate(state: any): any {
                return new Promise<any>((resolve: any) => { state.release = resolve; });
            }
            async function run(state: any, wait: any) {
                const iterator: any = compare(state, wait);
                const first: any = iterator.next();
                state.value = 6; state.release(null);
                console.log((await first).value);
                state.value = 7;
                console.log((await iterator.next(null)).value);
                state.value = 8;
                console.log((await iterator.next(0)).done);
            }
            const state: any = { value: null };
            run(state, gate(state)).then(() => {}, (error: any) => console.log("rejected", error));
            """, "async-generator-await true 6\nstrict\nasync-generator true 7\nloose\nasync-generator-loose true 8\ntrue\n"];
    }

    public static IEnumerable<object[]> InterpretedCases() => Cases().Where(row =>
        // The async-generator delegated-await caller-scope boundary is independent of compiled equality.
        (string)row[0] != "async-generator-save-left-across-await-and-yield");

    public static IEnumerable<object[]> ModeCases() => InterpretedCases()
        .Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Interpreted })
        .Concat(Cases().Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Compiled }));

    [Theory, MemberData(nameof(ModeCases))]
    public void EqualityUsesActualValuesAcrossNumericAnnotations(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
