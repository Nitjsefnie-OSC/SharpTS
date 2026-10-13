using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncIteratorMethodValidationTests
{
    [Theory, ModeData]
    public void NoncallableMethodsRejectBeforeSyncFallback(ExecutionMode mode)
    {
        const string source = """
            const iterable: any = { *[Symbol.iterator]() { yield 42; } };
            class C { *[Symbol.iterator]() { yield 42; } }
            const instance: any = new C();
            async function run() {
                for (const target of [iterable, instance]) {
                    for (const method of [1, false, "", {}]) {
                        target[Symbol.asyncIterator] = method;
                        try {
                            for await (const value of target) console.log(value);
                        } catch (error) { console.log(error instanceof TypeError); }
                    }
                    for (const method of [null, undefined]) {
                        target[Symbol.asyncIterator] = method;
                        for await (const value of target) console.log(value);
                    }
                }
            }
            run();
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true\ntrue\ntrue\ntrue\n42\n42\n", 2)),
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void CallableMethodRetainsReceiverAndSkipsSyncMethod(ExecutionMode mode)
    {
        const string source = """
            const iterable: any = {
                value: 43,
                async *[Symbol.asyncIterator]() { yield this.value; },
                *[Symbol.iterator]() { yield 42; }
            };
            async function run() { for await (const value of iterable) console.log(value); }
            run();
            """;
        Assert.Equal("43\n", TestHarness.Run(source, mode));
    }
}
