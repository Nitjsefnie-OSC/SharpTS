using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GenericArrowAdjacencyTests
{
    [Theory, ModeData]
    public void AdjacentAndSpacedAsyncReturnsProduceTheSameValue(ExecutionMode mode)
    {
        const string source = """
            const adjacent = async (): Promise<number>=> 47;
            const spaced = async (): Promise<number> => 47;
            async function main() { console.log(await adjacent(), await spaced()); }
            main();
            """;
        Assert.Equal("47 47\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NestedGenericReturnsAndGenericArrowsRun(ExecutionMode mode)
    {
        const string source = """
            const one = (): Array<number>=> [1];
            const two = (): Array<Array<number>>=> [[2]];
            const three = (): Array<Array<Array<number>>>=> [[[3]]];
            const four = (): Array<Array<Array<Array<number>>>>=> [[[[4]]]];
            const generic = <T>(value: T): Array<T>=> [value];
            console.log(one()[0], two()[0][0], three()[0][0][0], four()[0][0][0][0], generic<number>(5)[0]);
            const asyncGeneric = async <T>(value: T): Promise<Array<Array<T>>>=> [[value]];
            async function main() { console.log((await asyncGeneric<number>(6))[0][0]); }
            main();
            """;
        Assert.Equal("1 2 3 4 5\n6\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void RelationalShiftAndAssignmentOperatorsStillRun(ExecutionMode mode)
    {
        const string source = """
            interface Box<T> { value: T; }
            let boxed: Box<number>={ value: 7 };
            let right = 64;
            right >>= 2;
            let unsigned = -1;
            unsigned >>>= 28;
            const make = (): Array<number>=> [boxed.value];
            console.log(3 >= 2, right, unsigned, 32 >> 2, -1 >>> 30, make()[0]);
            """;
        Assert.Equal("true 16 15 8 3 7\n", TestHarness.Run(source, mode));
    }
}
