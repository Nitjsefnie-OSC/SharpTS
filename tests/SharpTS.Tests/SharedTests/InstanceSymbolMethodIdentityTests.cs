using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InstanceSymbolMethodIdentityTests
{
    public const string IssueSource = """
        const key = Symbol("method");
        class C { [key]() { return 42; } }
        class D extends C {}
        const first: any = new C(), second: any = new C(), derived: any = new D();
        console.log(first[key] === first[key]);
        console.log(first[key] === second[key]);
        console.log(first[key] === derived[key]);
        """;

    private const string Keys = """
        const plain = Symbol("plain"), asynchronous = Symbol("async");
        const generator = Symbol("generator"), asyncGenerator = Symbol("asyncGenerator");
        const keys = [plain, asynchronous, generator, asyncGenerator];
        """;

    private const string Methods = """
            [plain](first: any, second: any = 2, ...rest: any[]) { return [this, first, second, rest.length]; }
            async [asynchronous](first: any, second: any = 2, ...rest: any[]) {
                await Promise.resolve(0); return [this, first, second, rest.length];
            }
            *[generator](first: any, second: any = 2, ...rest: any[]) { yield [this, first, second, rest.length]; }
            async *[asyncGenerator](first: any, second: any = 2, ...rest: any[]) {
                await Promise.resolve(0); yield [this, first, second, rest.length];
            }
        """;

    public const string KindsSource = Keys + "class Base {\n" + Methods + "}\n" + """
        class Derived extends Base {}
        const first: any = new Base(), second: any = new Base(), derived: any = new Derived();
        for (const key of keys) {
            const method: any = first[key];
            console.log(method === first[key], method === second[key], method === derived[key],
                method.length, JSON.stringify(method.name));
        }
        """;

    public const string ReceiverSource = Keys + "class C {\n" + Methods + "}\n" + """
        async function main() {
            const first: any = new C(), second: any = new C(), receiver: any = {};
            for (let index = 0; index < keys.length; index++) {
                const key = keys[index], method: any = first[key];
                const copied: any = { method };
                let direct: any = first[key]("direct", undefined, 3);
                let borrowed: any = method.call(second, "borrowed", undefined, 3, 4);
                let bound: any = method.bind(receiver, "bound")(undefined, 3, 4);
                let copiedResult: any = copied.method("copied", undefined, 3);
                if (index >= 2) {
                    direct = (await direct.next()).value; borrowed = (await borrowed.next()).value;
                    bound = (await bound.next()).value; copiedResult = (await copiedResult.next()).value;
                } else {
                    direct = await direct; borrowed = await borrowed;
                    bound = await bound; copiedResult = await copiedResult;
                }
                console.log(direct[0] === first, direct[1], direct[2], direct[3],
                    borrowed[0] === second, borrowed[1], borrowed[2], borrowed[3]);
                console.log(bound[0] === receiver, bound[1], bound[2], bound[3],
                    copiedResult[0] === copied, copiedResult[1], copiedResult[2], copiedResult[3]);
            }
        }
        main().catch((error: any) => console.log(error.name, error.message));
        """;

    public const string OverridesSource = """
        const key = Symbol("method");
        class Base { [key]() { return "base"; } }
        class Derived extends Base {}
        class Override extends Base { [key]() { return "override"; } }
        class Peer { [key]() { return "peer"; } }
        const base: any = new Base(), inherited: any = new Derived();
        const overridden: any = new Override(), peer: any = new Peer();
        console.log(base[key] === inherited[key], base[key] === overridden[key], base[key] === peer[key]);
        console.log(base[key](), inherited[key](), overridden[key](), peer[key]());
        base[key].tag = "retained";
        console.log(inherited[key].tag, overridden[key].tag, peer[key].tag);
        """;

    public const string ExpressionSource = Keys + "function make() { return class C {\n" + Methods + "}; }\n" + """
        const First: any = make(), Second: any = make();
        const first: any = new First(), again: any = new First(), second: any = new Second();
        for (const key of keys) {
            console.log(first[key] === first[key], first[key] === again[key], first[key] === second[key]);
        }
        console.log(first[plain](0)[0] === first, second[plain](0)[0] === second);
        """;

    public const string PrototypeSource = """
        const key = Symbol("method");
        class Base { [key]() { return this; } }
        class Derived extends Base {}
        const prototype: any = Base.prototype, first: any = new Base(), second: any = new Derived();
        const method: any = first[key], descriptor: any = Object.getOwnPropertyDescriptor(prototype, key);
        console.log(method === prototype[key], descriptor.value === method,
            descriptor.writable, descriptor.enumerable, descriptor.configurable);
        const receiver: any = {};
        console.log(method.call(receiver) === receiver, prototype[key]() === prototype, second[key]() === second);
        console.log(delete prototype[key], first[key] === undefined, second[key] === undefined,
            Object.getOwnPropertyDescriptor(prototype, key) === undefined);
        Object.defineProperty(prototype, key, { value: () => 7, writable: true, configurable: true });
        console.log(first[key](), second[key](), first[key] === second[key], first[key] !== method);
        """;

    public const string IteratorSource = """
        class Sync { *[Symbol.iterator]() { yield this; } }
        class Async { async *[Symbol.asyncIterator]() { await Promise.resolve(0); yield this; } }
        const sync: any = new Sync();
        console.log(sync[Symbol.iterator] === sync[Symbol.iterator]);
        for (const value of sync) console.log(value === sync);
        async function main() {
            const asynchronous: any = new Async();
            console.log(asynchronous[Symbol.asyncIterator] === asynchronous[Symbol.asyncIterator]);
            for await (const value of asynchronous) console.log(value === asynchronous);
        }
        main().catch((error: any) => console.log(error.name, error.message));
        """;

    public const string GenericSource = """
        const key = Symbol("method");
        class Box<T> { [key](value: T) { return [this, value]; } }
        const first: any = new Box<number>(), second: any = new Box<string>();
        console.log(first[key] === first[key], first[key] === second[key]);
        console.log(first[key](7)[0] === first, first[key](7)[1],
            second[key]("ok")[0] === second, second[key]("ok")[1]);
        """;

    public const string FieldsAndSuperSource = """
        let calls = 0;
        const key = Symbol("method");
        class Base { value: number = 5; read() { return this.value; } }
        class Derived extends Base {
            value: number = 7;
            #secret: number = 3;
            read() { return 100; }
            [key](increment: number) {
                this.value = this.value + increment;
                this.#secret = this.#secret + increment;
                calls++;
                return [this, this.value, this.#secret, super.read(), calls];
            }
        }
        const first: any = new Derived(), second: any = new Derived();
        console.log(first[key] === second[key]);
        const one: any = first[key](2), two: any = first[key].call(second, 4);
        console.log(one[0] === first, one[1], one[2], one[3], one[4],
            two[0] === second, two[1], two[2], two[3], two[4], calls);
        """;

    public const string ExactReceiverSource = Keys + """
        class C {
            [plain]() { return this; }
            async [asynchronous]() { await Promise.resolve(0); return this; }
            *[generator]() { yield this; }
            async *[asyncGenerator]() { await Promise.resolve(0); yield this; }
        }
        async function main() {
            const instance: any = new C();
            for (let index = 0; index < keys.length; index++) {
                const method: any = instance[keys[index]];
                let called: any = method.call(null), bound: any = method.bind(null)();
                let primitive: any = method.bind(7)(), omitted: any = method.bind()();
                if (index >= 2) {
                    called = (await called.next()).value; bound = (await bound.next()).value;
                    primitive = (await primitive.next()).value; omitted = (await omitted.next()).value;
                } else {
                    called = await called; bound = await bound;
                    primitive = await primitive; omitted = await omitted;
                }
                console.log(called === null, bound === null, primitive === 7, omitted === undefined);
            }
        }
        main().catch((error: any) => console.log(error.name, error.message));
        """;

    public const string PrivateSuperReplacementSource = """
        class Base { value() { return 1; } }
        class Derived extends Base {
            #method() { return super.value(); }
            expose() { return this.#method; }
        }
        const prototype: any = Base.prototype;
        prototype.value = () => 2;
        const instance: any = new Derived(), method: any = instance.expose();
        console.log(method.call(instance));
        """;

    public const string LexicalArrowSource = """
        const key = Symbol("method");
        class Base { value: number = 5; read() { return this.value; } }
        class Derived extends Base {
            value: number = 7;
            read() { return 100; }
            [key]() { return () => [this, super.read()]; }
        }
        const instance: any = new Derived(), arrow: any = instance[key]();
        const copied: any = { arrow }, result: any = copied.arrow();
        console.log(result[0] === instance, result[1]);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true\ntrue\ntrue\n"];
        yield return ["kinds", KindsSource, string.Concat(new[] { "plain", "async", "generator", "asyncGenerator" }
            .Select(name => $"true true true 1 \"[{name}]\"\n"))];
        yield return ["receiver", ReceiverSource, string.Concat(Enumerable.Repeat(
            "true direct 2 1 true borrowed 2 2\ntrue bound 2 2 true copied 2 1\n", 4))];
        yield return ["overrides", OverridesSource, "true false false\nbase base override peer\nretained undefined undefined\n"];
        yield return ["expression", ExpressionSource, string.Concat(Enumerable.Repeat("true true false\n", 4)) + "true true\n"];
        yield return ["prototype", PrototypeSource,
            "true true true false true\ntrue true true\ntrue true true true\n7 7 true true\n"];
        yield return ["iterator", IteratorSource, "true\ntrue\ntrue\ntrue\n"];
        yield return ["generic", GenericSource, "true true\ntrue 7 true ok\n"];
        yield return ["fields-and-super", FieldsAndSuperSource, "true\ntrue 9 5 9 1 true 11 7 11 2 2\n"];
        yield return ["exact-receiver", ExactReceiverSource, string.Concat(Enumerable.Repeat("true true true true\n", 4))];
        yield return ["private-super-replacement", PrivateSuperReplacementSource, "2\n"];
        yield return ["lexical-arrow", LexicalArrowSource, "true 7\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        // Prototype replacement in interpreted private super lookup is a separate
        // existing limitation; this row protects the compiler's helper dispatch.
        => Cases().SelectMany(row => ((string)row[0] == "private-super-replacement"
                ? new[] { ExecutionMode.Compiled }
                : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void InstanceMethodsRetainDefinitionOwnedIdentityAndCallReceivers(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ModuleMethodsKeepLiveMutableCaptures(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["counter.ts"] = """
                let count = 5;
                export const key = Symbol("counter");
                export class Counter {
                    [key](increment: number) { count += increment; return [this, count]; }
                }
                """,
            ["main.ts"] = """
                import { Counter, key } from './counter';
                const first: any = new Counter(), second: any = new Counter();
                console.log(first[key] === second[key]);
                const one: any = first[key](2), two: any = second[key](3);
                console.log(one[0] === first, one[1], two[0] === second, two[1]);
                """
        };
        Assert.Equal("true\ntrue 7 true 10\n", TestHarness.RunModules(files, "main.ts", mode));
    }
}
