using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BlockGeneratorExpressionTests
{
    public const string ExactSyncSource = """
        let generator: any;
        {
            const value = 42;
            generator = function* named() { yield value; };
        }
        console.log(generator().next().value);
        """;

    public const string ExactAsyncSource = """
        let generator: any;
        {
            const value = 42;
            generator = async function* named() { yield value; };
        }
        async function run() { console.log((await generator().next()).value); }
        run();
        """;

    public const string LiveSyncSource = """
        function make(): any {
            {
                let value = 1;
                const read = () => value;
                const write = (next: number) => { value = next; };
                const generator: any = function* named(): Generator<number, void, number> {
                    const step: any = yield value;
                    value += step;
                    yield value;
                };
                return { generator, read, write };
            }
        }
        const api = make();
        api.write(3);
        const first = api.generator();
        api.write(4);
        console.log(first.next().value, api.read());
        api.write(10);
        console.log(first.next(2).value, api.read());
        const second = api.generator();
        api.write(20);
        console.log(second.next().value, first.next().done);
        console.log(second.next(3).value, api.read());
        """;

    public const string LiveAsyncSource = """
        function make(): any {
            {
                let value = 1;
                const read = () => value;
                const write = (next: number) => { value = next; };
                const generator: any = async function* named(): AsyncGenerator<number, void, number> {
                    const step: any = yield value;
                    await Promise.resolve(0);
                    value += step;
                    yield value;
                };
                return { generator, read, write };
            }
        }
        async function run() {
            const api = make();
            api.write(3);
            const first = api.generator();
            api.write(4);
            console.log((await first.next()).value, api.read());
            api.write(10);
            console.log((await first.next(2)).value, api.read());
            const second = api.generator();
            api.write(20);
            console.log((await second.next()).value, (await first.next()).done);
            console.log((await second.next(3)).value, api.read());
        }
        run();
        """;

    public const string EvaluationIdentitySource = """
        class Maker {
            make(seed: number): any {
                {
                    const value = seed;
                    return function* named(this: any, increment: number) {
                        yield named;
                        yield value + increment + this.offset;
                    };
                }
            }
        }
        const maker = new Maker();
        const first = maker.make(1);
        const second = maker.make(2);
        const left = first.call({ offset: 100 }, 10);
        const holder: any = { offset: 200, generator: second };
        const right = holder.generator(20);
        console.log(first !== second, first.prototype !== second.prototype,
            Object.getPrototypeOf(left) === first.prototype, left.next().value === first);
        console.log(left.next().value, right.next().value === second, right.next().value);
        console.log(first.name, second.name, first.length, second.length);
        const secondPrototype = second.prototype;
        const marker = {};
        Object.defineProperty(first, "prototype", { value: marker });
        console.log(first.prototype === marker, second.prototype === secondPrototype,
            Object.getPrototypeOf(first(0)) === marker);
        Object.freeze(first);
        console.log(Object.getOwnPropertyDescriptor(first, "prototype")!.writable,
            Object.getOwnPropertyDescriptor(second, "prototype")!.writable);
        """;

    public const string InitializationAndShadowSource = """
        let generator: any;
        let value = 100;
        const outside = () => value;
        {
            generator = function* named() { yield value; };
            const before = generator();
            try { before.next(); } catch (error: any) { console.log(error.name); }
            let value = 42;
            console.log(generator().next().value, outside());
        }
        """;

    public const string InvocationSource = """
        let effects = 0;
        let generator: any;
        {
            const cap = 40;
            generator = function* named(this: any, value: number = (effects++, cap), ...rest: any[]): Generator<string, number, number> {
                try {
                    const sent: any = yield [this.offset, value, rest.join(","), arguments.length].join("|");
                    return sent + cap;
                } finally { console.log("finally"); }
            };
        }
        console.log(generator.name, generator.length, effects);
        const iterator = generator.call({ offset: 2 }, undefined, 3, 4);
        console.log(effects);
        console.log(iterator.next().value);
        const completed = iterator.next(5);
        console.log(completed.value, completed.done);
        """;

    public const string SuspendedOwnerSource = """
        function* outer() {
            {
                let value = 1;
                const read = () => value;
                const generator = function*() {
                    yield value;
                    value += 1;
                    yield value;
                };
                yield { generator, read };
                value = 10;
                yield value;
            }
        }
        const owner = outer();
        const api: any = owner.next().value;
        const inner = api.generator();
        console.log(inner.next().value, api.read());
        console.log(owner.next().value, api.read());
        console.log(inner.next().value, api.read());
        console.log(owner.next().done, api.generator().next().value, api.read());
        """;

    public const string FreeOwnerRelayShadowSource = """
        function make(seed: number): any {
            var count = 1;
            {
                const cap = 10;
                const generator: any = function* named() {
                    yield seed + count + cap;
                    { const seed: number = 100; yield seed; }
                    seed += 1;
                    count += 2;
                    yield seed + count + cap;
                };
                return {
                    generator,
                    read: () => seed + count,
                    write: (nextSeed: number, nextCount: number) => { seed = nextSeed; count = nextCount; }
                };
            }
        }
        async function run() {
            const api = make(2);
            api.write(3, 4);
            const iterator = api.generator();
            api.write(5, 6);
            console.log((await iterator.next()).value, api.read());
            api.write(7, 8);
            console.log((await iterator.next()).value, api.read());
            console.log((await iterator.next()).value, api.read());
            console.log((await iterator.next()).done, api.read());
        }
        run();
        """;

    public const string NestedAsyncArrowOwnerSource = """
        async function make(seed: number): Promise<any> {
            const build = async () => {
                let api: any;
                {
                    let value = 1;
                    const read = () => value;
                    const generator: any = function* named() {
                        yield value + seed;
                        value += 3;
                        yield value + seed;
                    };
                    await Promise.resolve(0);
                    value = 2;
                    api = { generator, read };
                }
                return api;
            };
            const pending = build();
            seed = seed + 1;
            return await pending;
        }
        async function run() {
            const api = await make(40);
            const iterator = api.generator();
            console.log(iterator.next().value, api.read());
            console.log(iterator.next().value, api.read());
        }
        run();
        """;

    public const string FreeHeaderShadowSyncSource = """
        function make(seed: number): any {
            {
                const cap = 10;
                return function* named() {
                    yield seed + cap;
                    for (const seed of [100]) yield seed;
                    yield seed + cap;
                    for (const seed in { k: 1 }) yield seed;
                    yield seed + cap;
                };
            }
        }
        const iterator = make(2)();
        console.log(iterator.next().value, iterator.next().value, iterator.next().value,
            iterator.next().value, iterator.next().value);
        """;

    public const string FreeHeaderShadowAsyncSource = """
        function make(seed: number): any {
            {
                const cap = 10;
                return async function* named() {
                    yield seed + cap;
                    for (const seed of [100]) yield seed;
                    yield seed + cap;
                    for (const seed in { k: 1 }) yield seed;
                    yield seed + cap;
                };
            }
        }
        async function run() {
            const iterator = make(2)();
            console.log((await iterator.next()).value, (await iterator.next()).value,
                (await iterator.next()).value, (await iterator.next()).value,
                (await iterator.next()).value);
        }
        run();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-sync", ExactSyncSource, "42\n"];
        yield return ["exact-async", ExactAsyncSource, "42\n"];
        yield return ["live-sync", LiveSyncSource, "4 4\n12 12\n20 true\n23 23\n"];
        yield return ["live-async", LiveAsyncSource, "4 4\n12 12\n20 true\n23 23\n"];
        yield return ["evaluation-identity", EvaluationIdentitySource,
            "true true true true\n111 true 222\nnamed named 1 1\ntrue true true\nfalse true\n"];
        yield return ["evaluation-identity-async",
            "async function run() {\n" + EvaluationIdentitySource
                .Replace("return function* named", "return async function* named")
                .Replace("left.next().value", "(await left.next()).value")
                .Replace("right.next().value", "(await right.next()).value") + "\n}\nrun();",
            "true true true true\n111 true 222\nnamed named 1 1\ntrue true true\nfalse true\n"];
        yield return ["initialization-shadow", InitializationAndShadowSource, "ReferenceError\n42 100\n"];
        yield return ["invocation", InvocationSource, "named 0 0\n1\n2|40|3,4|3\nfinally\n45 true\n"];
        yield return ["suspended-owner", SuspendedOwnerSource, "1 1\n10 10\n11 11\ntrue 11 11\n"];
        yield return ["free-owner-relay-shadow-sync", FreeOwnerRelayShadowSource, "21 11\n100 15\n28 18\ntrue 18\n"];
        yield return ["free-owner-relay-shadow-async",
            FreeOwnerRelayShadowSource.Replace("function* named()", "async function* named()"),
            "21 11\n100 15\n28 18\ntrue 18\n"];
        yield return ["nested-async-arrow-owner", NestedAsyncArrowOwnerSource, "43 2\n46 5\n"];
        yield return ["free-header-shadow-sync", FreeHeaderShadowSyncSource, "12 100 12 k 12\n"];
        yield return ["free-header-shadow-async", FreeHeaderShadowAsyncSource, "12 100 12 k 12\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Interpreted })
        .Concat(Cases().Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Compiled }));

    [Theory, MemberData(nameof(ModeCases))]
    public void BlockCapturingGeneratorFunctionsRetainTheirOwnEnvironmentAndState(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        string output;
        if (mode == ExecutionMode.Compiled && (name == "evaluation-identity-async"))
        {
            var (errors, compiledOutput) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            output = compiledOutput;
        }
        else
        {
            output = TestHarness.Run(source, mode);
        }
        Assert.Equal(expected, output);
    }
}
