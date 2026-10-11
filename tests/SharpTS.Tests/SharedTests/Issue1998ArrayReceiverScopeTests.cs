using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class Issue1998ArrayReceiverScopeTests
{
    [Theory, ModeData]
    public void IndexedMapCanNestTypedSortsWithoutChangingItsReceiver(ExecutionMode mode)
    {
        const string source = """
            const values: any = [1, 2];
            const result = values['map'](function(value: number, index: number, array: any) {
                const defaultSort: string[] = ['b', 'a'];
                console.log(defaultSort.sort().join(','));
                const functionSort: number[] = [20, 10];
                console.log(functionSort.sort(function(left: number, right: number) {
                    return left - right;
                }).join(','));
                const arrowSort: number[] = [20, 10];
                console.log(arrowSort.sort((left: number, right: number) => {
                    return left - right;
                }).join(','));
                console.log(array === values, array[index] === value);
                return value;
            });
            console.log(result.join(','), values.join(','));
            """;
        const string iteration = "a,b\n10,20\n10,20\ntrue true\n";
        Assert.Equal(iteration + iteration + "1,2 1,2\n", TestHarness.Run(source, mode));
    }
}
