using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class TransitiveAsyncArrowStoreTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-grandchild-write", """
            async function main(): Promise<void> {
                let x: number = 5;
                const outer = async (): Promise<number> => {
                    const inner = async (): Promise<void> => { x = x * 2; };
                    await inner();
                    return x;
                };
                let result = await outer();
                console.log(result);
                console.log(x);
            }
            main();
            """, "10\n10\n"];
        yield return ["grandchild-await-and-siblings", """
            async function main() {
                let x = 1;
                const outer = async () => {
                    const first = async () => { await Promise.resolve(0); x += 2; };
                    const second = async () => { x *= 3; await Promise.resolve(0); x += 4; };
                    await first();
                    await second();
                    return x;
                };
                console.log(await outer(), x);
            }
            main();
            """, "13 13\n"];
        yield return ["direct-child-write", """
            async function main() { let n = 0; const write = async () => { n = 5; }; await write(); console.log(n); }
            main();
            """, "5\n"];
        yield return ["promoted-owner-with-parent-local-shadow", """
            async function main() {
                let x = 1;
                const writer = async () => {
                    const inner = async () => { x += 1; };
                    await inner();
                    return x;
                };
                const shadow = async () => {
                    let x = 10;
                    const inner = async () => x;
                    return await inner();
                };
                await writer();
                console.log(await shadow(), x);
            }
            main();
            """, "10 2\n"];
        yield return ["direct-write-with-grandchild-reader", """
            async function main() {
                let x = 1;
                const outer = async () => {
                    const inner = async () => x;
                    x = 5;
                    console.log(await inner());
                    x = 7;
                    console.log(await inner());
                };
                await outer();
                console.log(x);
            }
            main();
            """, "5\n7\n7\n"];
        yield return ["async-method-owner", """
            class C {
                async run() {
                    let x = 5;
                    const outer = async () => {
                        const inner = async () => { x *= 2; };
                        await inner();
                        return x;
                    };
                    console.log(await outer(), x);
                }
            }
            new C().run();
            """, "10 10\n"];
        yield return ["direct-child-awaited-write", """
            async function value(n: number): Promise<number> { return n; }
            async function main() {
                let total = 0;
                const inner = async () => { total = await value(5); };
                await inner();
                console.log(total);
            }
            main();
            """, "5\n"];
        yield return ["readonly-grandchild", """
            async function main() {
                let x = 5;
                const outer = async () => { const inner = async () => x * 2; return await inner(); };
                console.log(await outer(), x);
            }
            main();
            """, "10 5\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void TransitiveStoresShareVerifiableAsyncCaptureStorage(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
