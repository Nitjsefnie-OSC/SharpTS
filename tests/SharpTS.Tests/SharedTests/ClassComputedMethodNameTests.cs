using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassComputedMethodNameTests
{
    public const string IssueSource = """
        const key = Symbol();
        class C { static [key]() {} }
        const constructorValue: any = C;
        console.log(JSON.stringify(constructorValue[key].name));
        """;

    public const string KeyNamesSource = """
        const anonymous = Symbol(), empty = Symbol(""), described = Symbol("label"), known = Symbol.iterator;
        const keys = [anonymous, empty, described, known];
        class Declared {
            [anonymous](first: any, ...rest: any[]) {}
            [empty](first: any, ...rest: any[]) {}
            [described](first: any, ...rest: any[]) {}
            [known](first: any, ...rest: any[]) {}
            static [anonymous](first: any, ...rest: any[]) {}
            static [empty](first: any, ...rest: any[]) {}
            static [described](first: any, ...rest: any[]) {}
            static [known](first: any, ...rest: any[]) {}
        }
        const Expression: any = class Named {
            [anonymous](first: any, ...rest: any[]) {}
            [empty](first: any, ...rest: any[]) {}
            [described](first: any, ...rest: any[]) {}
            [known](first: any, ...rest: any[]) {}
            static [anonymous](first: any, ...rest: any[]) {}
            static [empty](first: any, ...rest: any[]) {}
            static [described](first: any, ...rest: any[]) {}
            static [known](first: any, ...rest: any[]) {}
        };
        for (const constructor of [Declared, Expression]) {
            const owner: any = constructor, instance: any = new owner();
            for (const key of keys) {
                const method: any = owner[key], instanceMethod: any = instance[key];
                const bound: any = method.bind({}, 1);
                console.log(JSON.stringify(method.name), JSON.stringify(instanceMethod.name), method.length,
                    instanceMethod.length, method === owner[key], JSON.stringify(bound.name), bound.length);
            }
        }
        """;

    public const string KeyNamesExpected = "\"\" \"\" 1 1 true \"bound \" 0\n"
        + "\"[]\" \"[]\" 1 1 true \"bound []\" 0\n"
        + "\"[label]\" \"[label]\" 1 1 true \"bound [label]\" 0\n"
        + "\"[Symbol.iterator]\" \"[Symbol.iterator]\" 1 1 true \"bound [Symbol.iterator]\" 0\n";

    public const string KindsSource = """
        const plain = Symbol(), asynchronous = Symbol(), generator = Symbol(), asyncGenerator = Symbol();
        const keys = [plain, asynchronous, generator, asyncGenerator];
        class Declared {
            [plain](first: any, ...rest: any[]) { return this; }
            async [asynchronous](first: any, ...rest: any[]) { await Promise.resolve(0); return this; }
            *[generator](first: any, ...rest: any[]) { yield this; }
            async *[asyncGenerator](first: any, ...rest: any[]) { await Promise.resolve(0); yield this; }
            static [plain](first: any, ...rest: any[]) { return this; }
            static async [asynchronous](first: any, ...rest: any[]) { await Promise.resolve(0); return this; }
            static *[generator](first: any, ...rest: any[]) { yield this; }
            static async *[asyncGenerator](first: any, ...rest: any[]) { await Promise.resolve(0); yield this; }
        }
        const Expression: any = class Named {
            [plain](first: any, ...rest: any[]) { return this; }
            async [asynchronous](first: any, ...rest: any[]) { await Promise.resolve(0); return this; }
            *[generator](first: any, ...rest: any[]) { yield this; }
            async *[asyncGenerator](first: any, ...rest: any[]) { await Promise.resolve(0); yield this; }
            static [plain](first: any, ...rest: any[]) { return this; }
            static async [asynchronous](first: any, ...rest: any[]) { await Promise.resolve(0); return this; }
            static *[generator](first: any, ...rest: any[]) { yield this; }
            static async *[asyncGenerator](first: any, ...rest: any[]) { await Promise.resolve(0); yield this; }
        };
        async function run() {
            for (const constructor of [Declared, Expression]) {
                const owner: any = constructor, instance: any = new owner(), receiver: any = {};
                for (const key of keys) {
                    const method: any = owner[key], instanceMethod: any = instance[key];
                    const bound: any = method.bind(receiver, 1);
                    console.log(JSON.stringify(method.name), JSON.stringify(instanceMethod.name), method.length,
                        instanceMethod.length, method === owner[key], JSON.stringify(bound.name), bound.length);
                }
                console.log(owner[plain].bind(receiver, 1)() === receiver,
                    (await owner[asynchronous].bind(receiver, 1)()) === receiver,
                    owner[generator].bind(receiver, 1)().next().value === receiver,
                    (await owner[asyncGenerator].bind(receiver, 1)().next()).value === receiver);
            }
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string IdentitySource = """
        const plain = Symbol("plain"), asynchronous = Symbol("async"), generator = Symbol("generator"), asyncGenerator = Symbol("asyncGenerator");
        const keys = [plain, asynchronous, generator, asyncGenerator];
        class Base {
            static [plain](first: any, ...rest: any[]) {}
            static async [asynchronous](first: any, ...rest: any[]) {}
            static *[generator](first: any, ...rest: any[]) {}
            static async *[asyncGenerator](first: any, ...rest: any[]) {}
        }
        class Derived extends Base {}
        function make() {
            return class {
                static [plain](first: any, ...rest: any[]) {}
                static async [asynchronous](first: any, ...rest: any[]) {}
                static *[generator](first: any, ...rest: any[]) {}
                static async *[asyncGenerator](first: any, ...rest: any[]) {}
            };
        }
        const base: any = Base, derived: any = Derived, first: any = make(), second: any = make();
        for (const key of keys) {
            console.log(base[key] === base[key], derived[key] === base[key], first[key] === first[key],
                first[key] === second[key], JSON.stringify(first[key].name), first[key].length,
                JSON.stringify(first[key].bind({}, 1).name));
        }
        """;

    public const string IdentityExpected = "true true true false \"[plain]\" 1 \"bound [plain]\"\n"
        + "true true true false \"[async]\" 1 \"bound [async]\"\n"
        + "true true true false \"[generator]\" 1 \"bound [generator]\"\n"
        + "true true true false \"[asyncGenerator]\" 1 \"bound [asyncGenerator]\"\n";

    public const string MutationSource = """
        const key = Symbol("initial");
        class Declared { static [key](first: any) { return this; } }
        const Expression: any = class { static [key](first: any) { return this; } };
        for (const constructor of [Declared, Expression]) {
            const owner: any = constructor, method: any = owner[key], receiver: any = {};
            console.log(JSON.stringify(method.name));
            Object.defineProperty(method, "name", { value: "renamed", configurable: true });
            const bound: any = owner[key].bind(receiver, 1);
            console.log(method === owner[key], JSON.stringify(owner[key].name), JSON.stringify(bound.name),
                method.length, bound.length, bound() === receiver);
            Object.defineProperty(method, "name", { value: "later", configurable: true });
            console.log(JSON.stringify(bound.name), JSON.stringify(owner[key].name),
                JSON.stringify(owner[key].bind({}).name), JSON.stringify(bound.bind(null).name));
        }
        """;

    public const string EvaluationSource = """
        let evaluations = 0;
        function key(value: any) { evaluations++; return value; }
        const anonymous = Symbol(), described = Symbol("evaluated");
        class Declared {
            [key(anonymous)]() {}
            static [key(described)](first: any) {}
        }
        const Expression: any = class { [key(anonymous)]() {} static [key(described)](first: any) {} };
        console.log(evaluations);
        for (const constructor of [Declared, Expression]) {
            const owner: any = constructor, instance: any = new owner();
            console.log(JSON.stringify(instance[anonymous].name), JSON.stringify(owner[described].name),
                JSON.stringify(owner[described].bind({}).name), owner[described] === owner[described], evaluations);
        }
        let current = Symbol("before");
        const original = current;
        class Held { static [current]() {} }
        current = Symbol("after");
        const owner: any = Held;
        console.log(JSON.stringify(owner[original].name), typeof owner[current], evaluations);
        """;

    public const string EvaluationExpected = "4\n\"\" \"[evaluated]\" \"bound [evaluated]\" true 4\n"
        + "\"\" \"[evaluated]\" \"bound [evaluated]\" true 4\n\"[before]\" undefined 4\n";

    public const string NamedSource = """
        class Declared {
            named(first: any, ...rest: any[]) {}
            UPPER(first: any, ...rest: any[]) {}
            static named(first: any, ...rest: any[]) {}
            static UPPER(first: any, ...rest: any[]) {}
        }
        const Expression: any = class {
            named(first: any, ...rest: any[]) {}
            UPPER(first: any, ...rest: any[]) {}
            static named(first: any, ...rest: any[]) {}
            static UPPER(first: any, ...rest: any[]) {}
        };
        for (const constructor of [Declared, Expression]) {
            const owner: any = constructor, instance: any = new owner();
            for (const key of ["named", "UPPER"]) {
                const method: any = owner[key], instanceMethod: any = instance[key];
                console.log(JSON.stringify(method.name), JSON.stringify(instanceMethod.name), method.length,
                    instanceMethod.length, JSON.stringify(method.bind({}, 1).name));
            }
        }
        """;

    public const string NamedExpected = "\"named\" \"named\" 1 1 \"bound named\"\n"
        + "\"UPPER\" \"UPPER\" 1 1 \"bound UPPER\"\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "\"\"\n"];
        yield return ["key-names", KeyNamesSource, KeyNamesExpected + KeyNamesExpected];
        var kindRows = string.Concat(Enumerable.Repeat("\"\" \"\" 1 1 true \"bound \" 0\n", 4)) + "true true true true\n";
        yield return ["kinds", KindsSource, kindRows + kindRows];
        yield return ["identity", IdentitySource, IdentityExpected];
        var mutationRows = "\"[initial]\"\ntrue \"renamed\" \"bound renamed\" 1 0 true\n"
            + "\"bound renamed\" \"later\" \"bound later\" \"bound bound renamed\"\n";
        yield return ["mutation", MutationSource, mutationRows + mutationRows];
        yield return ["evaluation", EvaluationSource, EvaluationExpected];
        yield return ["named", NamedSource, NamedExpected + NamedExpected];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ClassMethodNamesUseEvaluatedPropertyKeys(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
