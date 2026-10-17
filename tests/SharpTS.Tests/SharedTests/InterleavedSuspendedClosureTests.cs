using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InterleavedSuspendedClosureTests
{
    public const string IssueSource = """
        let release: any;
        const gate = new Promise<any>((resolve: any) => { release = resolve; });
        class C {
            static async asynchronousBody(wait: any, label: any) { const values: any = [wait, label];
                const read = () => values;
                await wait;
                values[1] += "!";
                return read;
            }
            static *generatorBody(label: any) { const values: any = [label];
                const read = () => values;
                yield read;
                values[0] += "!";
                yield read;
            }
            static async *asyncGeneratorBody(wait: any, label: any) { const values: any = [wait, label];
                const read = () => values;
                await wait;
                yield read;
                await Promise.resolve(0);
                values[1] += "!";
                yield read;
            }
            static asynchronous(wait: any, label: any) { return this.asynchronousBody(wait, label); }
            static generator(label: any) { return this.generatorBody(label); }
            static asyncGenerator(wait: any, label: any) { return this.asyncGeneratorBody(wait, label); }
        }
        function intervening(...unused: any[]) { return arguments.length; }
        async function run() {
            const left = C.asynchronous(gate, "left"), right = C.asynchronous(gate, "right");
            const first = C.generator("first"), second = C.generator("second");
            const one = C.asyncGenerator(gate, "one"), two = C.asyncGenerator(gate, "two");
            const nextOne = one.next(), nextTwo = two.next();
            console.log(intervening(1, 2, 3));
            release(0);
            const leftRead: any = await left, rightRead: any = await right;
            console.log(leftRead()[1], rightRead()[1], leftRead().length, leftRead() !== rightRead());
            const firstRead: any = first.next().value, secondRead: any = second.next().value;
            console.log(firstRead()[0], secondRead()[0], firstRead() !== secondRead());
            const firstAgain: any = first.next().value, secondAgain: any = second.next().value;
            console.log(firstAgain()[0], secondAgain()[0], firstAgain() === firstRead(), secondAgain() === secondRead());
            const oneRead: any = (await nextOne).value, twoRead: any = (await nextTwo).value;
            console.log(oneRead()[1], twoRead()[1], oneRead().length, oneRead() !== twoRead());
            const oneAgain: any = (await one.next()).value, twoAgain: any = (await two.next()).value;
            console.log(oneAgain()[1], twoAgain()[1], oneAgain() === oneRead(), twoAgain() === twoRead());
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string ReverseReleaseSource = """
        let releaseLeft: any, releaseRight: any;
        const leftGate = new Promise<any>((resolve: any) => { releaseLeft = resolve; });
        const rightGate = new Promise<any>((resolve: any) => { releaseRight = resolve; });
        class C {
            async #body(wait: any, label: any) {
                const values: any = [label, 0];
                const read = () => values;
                await wait;
                values[0] += "!";
                values[1]++;
                return read;
            }
            read(wait: any, label: any) { return this.#body(wait, label); }
        }
        function unrelated(label: any) {
            const values: any = [label];
            return () => values;
        }
        async function run() {
            const first = new C(), second = new C();
            const left = first.read(leftGate, "left"), right = second.read(rightGate, "right");
            releaseRight(0);
            const rightRead: any = await right;
            const saved = rightRead();
            console.log(saved[0], saved[1], rightRead() === saved);
            const other = unrelated("outside");
            console.log(other()[0], rightRead() === saved);
            rightRead()[0] += "-changed";
            releaseLeft(0);
            const leftRead: any = await left;
            console.log(leftRead()[0], leftRead()[1], rightRead()[0],
                leftRead() !== rightRead(), rightRead() === saved);
            leftRead()[1] = 7;
            console.log(leftRead()[1], rightRead()[1]);
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string EqualityAwaitCallerSource = """
        function gate(state: any): any {
            return new Promise<any>((resolve: any) => { state.release = resolve; });
        }
        async function* values(wait: any): any {
            const same = (null as any) === await wait;
            console.log("same", same);
            yield 1;
        }
        async function run(state: any, wait: any) {
            const marker: any = { tag: "caller" };
            const iterator: any = values(wait);
            const pending: any = iterator.next();
            state.release(null);
            const first: any = await pending;
            console.log(marker.tag, first.value);
        }
        const state: any = {};
        run(state, gate(state)).then(() => {}, (error: any) => console.log("rejected", error.message));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource,
            "3\nleft! right! 2 true\nfirst second true\nfirst! second! true true\n" +
            "one two 2 true\none! two! true true\n"];
        yield return ["reverse-release", ReverseReleaseSource,
            "right! 1 true\noutside true\nleft! 1 right!-changed true true\n7 1\n"];
        yield return ["equality-await-caller", EqualityAwaitCallerSource, "same true\ncaller 1\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InterleavedSuspendedMethodsRetainTheirOwnCapturedArraysAndClosures(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
