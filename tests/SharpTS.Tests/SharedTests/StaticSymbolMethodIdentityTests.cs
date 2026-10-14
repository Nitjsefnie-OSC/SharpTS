using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticSymbolMethodIdentityTests
{
    public const string IssueSource = """
        const key = Symbol("method");
        class C { static [key]() { return 42; } }
        class D extends C {}
        const base: any = C, derived: any = D;
        console.log(base[key] === base[key]);
        console.log(base[key] === derived[key]);
        """;

    public const string MetadataSource = """
        const plain = Symbol("plain"), asynchronous = Symbol("async");
        const generator = Symbol("generator"), asyncGenerator = Symbol("asyncGenerator");
        class Base {
            static [plain](first: any, second: any = 2, ...rest: any[]) { return first; }
            static async [asynchronous](first: any, second: any = 2, ...rest: any[]) { return first; }
            static *[generator](first: any, second: any = 2, ...rest: any[]) { yield first; }
            static async *[asyncGenerator](first: any, second: any = 2, ...rest: any[]) { yield first; }
        }
        class Derived extends Base {}
        const base: any = Base, derived: any = Derived;
        for (const key of [plain, asynchronous, generator, asyncGenerator]) {
            const method: any = base[key];
            console.log(method === base[key], method === derived[key],
                base[key].length, derived[key].length);
        }
        """;

    public const string ReceiverSource = """
        const plain = Symbol("plain"), asynchronous = Symbol("async");
        const generator = Symbol("generator"), asyncGenerator = Symbol("asyncGenerator");
        class Base {
            static [plain](value: any) { return [this, value]; }
            static async [asynchronous](value: any) { await Promise.resolve(0); return [this, value]; }
            static *[generator](value: any) { yield [this, value]; }
            static async *[asyncGenerator](value: any) { await Promise.resolve(0); yield [this, value]; }
        }
        class Derived extends Base {}
        const base: any = Base, derived: any = Derived;
        const sync: any = base[plain], asyncFn: any = base[asynchronous];
        const gen: any = base[generator], asyncGen: any = base[asyncGenerator];
        const copied: any = { sync, asyncFn, gen, asyncGen };
        const direct: any = sync("bare");
        console.log(base[plain]("base")[0] === base, derived[plain]("derived")[0] === derived,
            direct[0] === undefined, direct[1], copied.sync("copied")[0] === copied);
        console.log(sync.call(null, 1)[0] === null, sync.apply(7, [2])[0] === 7,
            sync.bind(false, 3)()[0] === false);
        async function run() {
            const asyncBare: any = await asyncFn("bare");
            console.log((await base[asynchronous]("base"))[0] === base,
                (await derived[asynchronous]("derived"))[0] === derived,
                asyncBare[0] === undefined, asyncBare[1], (await copied.asyncFn("copied"))[0] === copied);
            console.log((await asyncFn.call(null, 1))[0] === null,
                (await asyncFn.apply(7, [2]))[0] === 7, (await asyncFn.bind(false, 3)())[0] === false);
            const genBare: any = gen("bare").next().value;
            console.log(base[generator]("base").next().value[0] === base,
                derived[generator]("derived").next().value[0] === derived,
                genBare[0] === undefined, genBare[1], copied.gen("copied").next().value[0] === copied);
            console.log(gen.call(null, 1).next().value[0] === null,
                gen.apply(7, [2]).next().value[0] === 7, gen.bind(false, 3)().next().value[0] === false);
            const asyncGenBare: any = (await asyncGen("bare").next()).value;
            console.log((await base[asyncGenerator]("base").next()).value[0] === base,
                (await derived[asyncGenerator]("derived").next()).value[0] === derived,
                asyncGenBare[0] === undefined, asyncGenBare[1], (await copied.asyncGen("copied").next()).value[0] === copied);
            console.log((await asyncGen.call(null, 1).next()).value[0] === null,
                (await asyncGen.apply(7, [2]).next()).value[0] === 7,
                (await asyncGen.bind(false, 3)().next()).value[0] === false);
        }
        run();
        """;

    public static string ReceiverExpected => string.Concat(Enumerable.Repeat(
        "true true true bare true\ntrue true true\n", 4));

    public const string CacheKeySource = """
        const first = Symbol("same"), second = Symbol("same");
        class Base {
            static [first](value: any, fallback: any = 2, ...rest: any[]) { return [this, value, fallback, rest.length]; }
            static [second](value: any) { return [this, value]; }
        }
        class Derived extends Base {}
        const base: any = Base, derived: any = Derived;
        const method: any = base[first];
        method.tag = "retained";
        Object.defineProperty(method, "name", { value: "changed", configurable: true });
        console.log(method === base[first], method === derived[first], method === base[second]);
        console.log(derived[first].tag, base[first].name, base[first].length, base[second].name);
        const result: any = derived[first](42, undefined, 43, 44);
        console.log(result[0] === derived, result[1], result[2], result[3]);
        """;

    public const string CacheKeyExpected = "true true false\nretained changed 1 [same]\ntrue 42 2 2\n";

    [Theory, ModeData]
    public void CachedReadsKeepDistinctEvaluatedSymbolsAndRetainMutableMetadata(ExecutionMode mode)
        => Assert.Equal(CacheKeyExpected, TestHarness.Run(CacheKeySource, mode));

    [Theory, ModeData]
    public void CachedMethodsPreserveDefaultRestLengths(ExecutionMode mode)
    {
        Assert.Equal(string.Concat(Enumerable.Repeat("true true 1 1\n", 4)),
            TestHarness.Run(MetadataSource, mode));
    }

    [Theory, ModeData]
    public void PlainAndAsyncMethodsRetainAssignedProperties(ExecutionMode mode)
    {
        const string source = """
            const plain = Symbol("plain"), asynchronous = Symbol("async");
            class Base {
                static [plain]() { return 1; }
                static async [asynchronous]() { return 1; }
            }
            class Derived extends Base {}
            const base: any = Base, derived: any = Derived;
            for (const key of [plain, asynchronous]) {
                const method: any = base[key];
                method.tag = "retained";
                console.log(method === base[key], method === derived[key], derived[key].tag);
            }
            """;
        Assert.Equal("true true retained\ntrue true retained\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void GeneratorMethodsRetainAssignedProperties(ExecutionMode mode)
    {
        const string source = """
            const generator = Symbol("generator"), asyncGenerator = Symbol("asyncGenerator");
            class Base {
                static *[generator]() { yield 1; }
                static async *[asyncGenerator]() { yield 1; }
            }
            class Derived extends Base {}
            const base: any = Base, derived: any = Derived;
            for (const key of [generator, asyncGenerator]) {
                const method: any = base[key];
                method.tag = "retained";
                console.log(method === base[key], method === derived[key], derived[key].tag);
            }
            """;
        Assert.Equal("true true retained\ntrue true retained\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void CanonicalMethodsReceiveThisAndArgumentsOnlyWhenCalled(ExecutionMode mode)
    {
        Assert.Equal(ReceiverExpected, TestHarness.Run(ReceiverSource, mode));
    }

    [Theory, ModeData]
    public void OverridesAndUnrelatedMethodsHaveDistinctIdentities(ExecutionMode mode)
    {
        const string source = """
            const key = Symbol("method"), other = Symbol("other");
            class Base { static [key]() { return "base"; } static [other]() { return "other"; } }
            class Child extends Base {}
            class Override extends Base { static [key]() { return "override"; } }
            class Peer { static [key]() { return "peer"; } }
            const base: any = Base, child: any = Child, overridden: any = Override, peer: any = Peer;
            console.log(base[key] === child[key], base[key] === overridden[key],
                base[key] === peer[key], base[key] === base[other]);
            console.log(base[key](), child[key](), overridden[key](), peer[key]());
            """;
        Assert.Equal("true false false false\nbase base override peer\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassExpressionMethodsKeepTheirDefinitionOwnedIdentities(ExecutionMode mode)
    {
        const string source = """
            const plain = Symbol("plain"), asynchronous = Symbol("async");
            const generator = Symbol("generator"), asyncGenerator = Symbol("asyncGenerator");
            function make() {
                return class {
                    static [plain]() { return this; }
                    static async [asynchronous]() { return this; }
                    static *[generator]() { yield this; }
                    static async *[asyncGenerator]() { yield this; }
                };
            }
            const first: any = make(), second: any = make();
            for (const key of [plain, asynchronous, generator, asyncGenerator]) {
                console.log(first[key] === first[key], first[key] === second[key]);
            }
            console.log(first[plain]() === first, second[plain]() === second);
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true false\n", 4)) + "true true\n",
            TestHarness.Run(source, mode));
    }
}
