using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrivateCompoundAssignmentTests
{
    public const string IssueSource = """
        class C {
            #value: number = 1;
            increase() { this.#value += 2; return this.#value; }
        }
        console.log(new C().increase());
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "3\n"];
        yield return ["operators-and-results", """
            class C {
                #value: any;
                record(value: any) { return [value, this.#value].join(":"); }
                run() {
                    const values: string[] = [];
                    this.#value = 8; values.push(this.record(this.#value += 2));
                    this.#value = 8; values.push(this.record(this.#value -= 3));
                    this.#value = 8; values.push(this.record(this.#value *= 3));
                    this.#value = 8; values.push(this.record(this.#value /= 2));
                    this.#value = 8; values.push(this.record(this.#value %= 3));
                    this.#value = 3; values.push(this.record(this.#value <<= 2));
                    this.#value = -16; values.push(this.record(this.#value >>= 2));
                    this.#value = -16; values.push(this.record(this.#value >>>= 2));
                    this.#value = 6; values.push(this.record(this.#value &= 3));
                    this.#value = 6; values.push(this.record(this.#value ^= 3));
                    this.#value = 4; values.push(this.record(this.#value |= 3));
                    console.log(values.join(","));
                    this.#value = "a"; console.log(this.record(this.#value += 2));
                    this.#value = undefined;
                    const result = this.#value += 1;
                    console.log(Number.isNaN(result), Number.isNaN(this.#value));
                    this.#value = -0;
                    const zero = this.#value /= 2;
                    console.log(Object.is(zero, -0), Object.is(this.#value, -0));
                }
            }
            new C().run();
            """, "10:10,5:5,24:24,4:4,2:2,12:12,-4:-4,1073741820:1073741820,2:2,5:5,7:7\na2:a2\ntrue true\ntrue true\n"];
        yield return ["receiver-once-and-saved-read", """
            let trace = "";
            class C {
                #value: number = 1;
                static run(first: C, second: C) {
                    let selected = first, receivers = 0, rightSides = 0;
                    const pick = () => { trace += "r"; receivers++; return selected; };
                    const result = pick().#value += (() => {
                        trace += "h"; rightSides++;
                        first.#value = 100;
                        selected = second;
                        return 2;
                    })();
                    console.log(result, first.#value, second.#value, receivers, rightSides, trace);
                }
            }
            C.run(new C(), new C());
            """, "3 3 1 1 1 rh\n"];
        yield return ["coercion-and-abrupt-completion", """
            let trace = "";
            class C {
                #value: any;
                constructor(value: any) { this.#value = value; }
                read() { return this.#value; }
                add(rhs: () => any) { return this.#value += rhs(); }
            }
            {
                const left: any = { valueOf() { trace += "l"; return 4; } };
                const right: any = { valueOf() { trace += "q"; return 3; } };
                const instance = new C(left);
                console.log(instance.add(() => { trace += "r"; return right; }), instance.read(), trace);
            }
            {
                trace = "";
                const token: any = {};
                const left: any = { valueOf() { trace += "l"; return 4; } };
                const instance = new C(left);
                try { instance.add(() => { trace += "r"; throw token; }); }
                catch (error) { console.log(error === token, instance.read() === left, trace); }
            }
            {
                trace = "";
                const token: any = {};
                const left: any = { valueOf() { trace += "l"; throw token; } };
                const right: any = { valueOf() { trace += "q"; return 3; } };
                const instance = new C(left);
                try { instance.add(() => { trace += "r"; return right; }); }
                catch (error) { console.log(error === token, instance.read() === left, trace); }
            }
            {
                trace = "";
                const token: any = {};
                const left: any = { valueOf() { trace += "l"; return 4; } };
                const right: any = { valueOf() { trace += "q"; throw token; } };
                const instance = new C(left);
                try { instance.add(() => { trace += "r"; return right; }); }
                catch (error) { console.log(error === token, instance.read() === left, trace); }
            }
            """, "7 7 rlq\ntrue true r\ntrue true rl\ntrue true rlq\n"];
        yield return ["brand-before-rhs", """
            let rights = 0;
            class C {
                #value = 1;
                read() { return this.#value; }
                static add(object: any) { return object.#value += (++rights); }
                static from(pick: () => any) { return pick().#value += (++rights); }
            }
            class Other { #value = 1; }
            const instance = new C();
            console.log(C.add(instance), instance.read(), rights);
            const wrong: any[] = [{ "#value": 1 }, new Other(), Object.create(C.prototype), null, undefined];
            for (const value of wrong) {
                try { C.add(value); console.log(false); }
                catch (error) { console.log(error instanceof TypeError, rights); }
            }
            class Early {
                #first = this.bump();
                #later = 1;
                bump() { return this.#later += (++rights); }
            }
            try { new Early(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError, rights); }
            try {
                class EarlyStatic {
                    static #first = EarlyStatic.bump();
                    static #later = 1;
                    static bump() { return EarlyStatic.#later += (++rights); }
                }
            }
            catch (error) { console.log(error instanceof TypeError, rights); }
            const token: any = {};
            try { C.from(() => { throw token; }); }
            catch (error) { console.log(error === token, rights); }
            """, "2 2 1\ntrue 1\ntrue 1\ntrue 1\ntrue 1\ntrue 1\ntrue 1\ntrue 1\ntrue 1\n"];
        yield return ["static-and-generic-owners", """
            let rights = 0;
            class Static {
                static #value = 1;
                static add(target: typeof Static) { return target.#value += (++rights); }
                static read() { return Static.#value; }
            }
            class Derived extends Static {}
            console.log(Static.add(Static), Static.read(), rights);
            try { Static.add(Derived as any); console.log(false); }
            catch (error) { console.log(error instanceof TypeError, rights, Static.read()); }
            class Generic<T> {
                #value = 3;
                #label: T;
                constructor(label: T) { this.#label = label; }
                add() { return this.#value += 2; }
                label() { return this.#label; }
            }
            const first = new Generic<number>(1), second = new Generic<string>("x");
            console.log(first.add(), second.add(), typeof first.label(), typeof second.label());
            """, "2 2 1\ntrue 1 2\n5 5 number string\n"];
        yield return ["evaluated-owners-and-private-only-arrows", """
            let rights = 0;
            function make(seed: number) {
                return class Named {
                    #value: number;
                    static #total = seed;
                    constructor() { this.#value = seed; }
                    adder() { return () => this.#value += 2; }
                    read() { return this.#value; }
                    addTo(other: any) { return other.#value += (++rights); }
                    static adder() { return () => Named.#total += 1; }
                    static read() { return Named.#total; }
                };
            }
            const A = make(7), B = make(9);
            const first = new A(), second = new B();
            const addFirst = first.adder(), addSecond = second.adder();
            const staticFirst = A.adder(), staticSecond = B.adder();
            console.log(addFirst(), addSecond(), addFirst(), first.read(), second.read());
            console.log(staticFirst(), staticSecond(), staticFirst(), A.read(), B.read());
            try { first.addTo(second); console.log(false); }
            catch (error) { console.log(error instanceof TypeError, rights); }
            console.log(first.addTo(first), first.read(), rights);
            """, "9 11 11 11 11\n8 10 9 9 10\ntrue 0\n12 12 1\n"];
        yield return ["suspended-reference-and-read", """
            class C {
                #value: number;
                constructor(value: number) { this.#value = value; }
                read() { return this.#value; }
                reset(value: number) { this.#value = value; }
                *change(pick: () => C): Generator<any, any, any> {
                    return pick().#value += ((yield "rhs") as any);
                }
                async changeAsync(pick: () => C, pending: Promise<number>) {
                    return pick().#value += await pending;
                }
                changeArrow(pick: () => C, pending: Promise<number>) {
                    return async () => pick().#value += await pending;
                }
                async *changeAsyncGenerator(pick: () => C): AsyncGenerator<any, any, any> {
                    return pick().#value += ((yield "rhs") as any);
                }
            }
            const first = new C(1), second = new C(11), other = new C(5), otherSecond = new C(15);
            let selected = first, otherSelected = other, picks = 0, otherPicks = 0;
            const iterator = first.change(() => { picks++; return selected; });
            const otherIterator = other.change(() => { otherPicks++; return otherSelected; });
            console.log(iterator.next().value, otherIterator.next().value, picks, otherPicks);
            first.reset(100); other.reset(200); selected = second; otherSelected = otherSecond;
            console.log(iterator.next(2).value, otherIterator.next(3).value,
                first.read(), other.read(), second.read(), otherSecond.read(), picks, otherPicks);
            async function runAsync() {
                const first = new C(1), second = new C(11);
                let selected = first, picks = 0;
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                const task = first.changeAsync(() => { picks++; return selected; }, pending);
                console.log("async", picks);
                first.reset(100); selected = second; release(2);
                console.log(await task, first.read(), second.read(), picks);
            }
            async function runArrow() {
                const arrowFirst = new C(4), arrowSecond = new C(14);
                let arrowSelected = arrowFirst, arrowPicks = 0;
                let releaseArrow: (value: number) => void = () => {};
                const arrowPending = new Promise<number>(resolve => { releaseArrow = resolve; });
                const operation = arrowFirst.changeArrow(() => { arrowPicks++; return arrowSelected; }, arrowPending);
                const arrowTask = operation();
                console.log("arrow", arrowPicks);
                arrowFirst.reset(400); arrowSelected = arrowSecond; releaseArrow(5);
                console.log(await arrowTask, arrowFirst.read(), arrowSecond.read(), arrowPicks);
            }
            async function runGenerator() {
                const generatedFirst = new C(6), generatedSecond = new C(16);
                let generatedSelected = generatedFirst, generatedPicks = 0;
                const generated = generatedFirst.changeAsyncGenerator(() => { generatedPicks++; return generatedSelected; });
                console.log("async-generator", (await generated.next()).value, generatedPicks);
                generatedFirst.reset(600); generatedSelected = generatedSecond;
                console.log((await generated.next(4)).value, generatedFirst.read(), generatedSecond.read(), generatedPicks);
            }
            async function run() {
                await runAsync();
                await runArrow();
                await runGenerator();
            }
            run();
            """, "rhs rhs 1 1\n3 8 3 8 11 15 1 1\nasync 1\n3 3 11 1\narrow 1\n9 9 14 1\nasync-generator rhs 1\n10 10 16 1\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void PrivateCompoundAssignmentsPreserveTheSavedReference(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
