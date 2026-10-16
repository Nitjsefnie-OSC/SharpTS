using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncArrowInstanceFieldTests
{
    public const string IssueSource = """
        class C {
            publicField = async () => 45;
            #privateField = async () => 46;
            read() { return this.#privateField; }
        }
        const value = new C();
        console.log(typeof value.publicField, typeof value.read());
        async function run() {
            console.log(await value.publicField());
            const fn: any = value.read();
            console.log(await fn());
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string ClassExpressionSource = """
        const C = class {
            #value = 0;
            publicField = async () => {
                await Promise.resolve(0);
                return this.#value;
            };
            #privateField = async () => {
                await Promise.resolve(0);
                return this.#value + 1;
            };
            constructor(value: number) { this.#value = value; }
            read() { return this.#privateField; }
        };
        const first = new C(45), second = new C(55);
        const firstPublic = first.publicField, secondPublic = second.publicField;
        const firstPrivate = first.read(), secondPrivate = second.read();
        console.log(typeof firstPublic, typeof firstPrivate, firstPublic !== secondPublic,
            firstPrivate !== secondPrivate, firstPrivate === first.read());
        async function run() {
            console.log(await firstPublic(), await firstPrivate(), await secondPublic(), await secondPrivate());
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "function function\n45\n46\n"];
        yield return ["expression-receivers", ClassExpressionSource,
            "function function true true true\n45 46 55 56\n"];
        yield return ["declaration-receivers", ClassExpressionSource.Replace("const C = class {", "class C {"),
            "function function true true true\n45 46 55 56\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AsyncArrowFieldsRemainCallableAndCaptureEachInstance(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
