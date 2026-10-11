using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class Issue1998ArraySuspensionTests
{
    [Theory, ModeData]
    public void FlatMapEvaluatesSuspendingCallbackAndThisArgOnce(ExecutionMode mode)
    {
        const string source = """
            let callbackFactories = 0;
            let contextFactories = 0;
            const events: string[] = [];
            async function makeCallback(): Promise<any> {
                callbackFactories++;
                events.push('callback');
                await Promise.resolve(0);
                return function(value: number) {
                    events.push('mapped');
                    return [value + this.offset];
                };
            }
            async function makeContext(): Promise<any> {
                contextFactories++;
                events.push('context');
                await Promise.resolve(0);
                return {offset: 10};
            }
            async function main() {
                const values: number[] = [1, 2];
                const result = values.flatMap(await makeCallback(), await makeContext());
                console.log(result.join(','));
                console.log(callbackFactories, contextFactories);
                console.log(events.join(','));
            }
            main();
            """;
        Assert.Equal("11,12\n1 1\ncallback,context,mapped,mapped\n", TestHarness.Run(source, mode));
    }
}
