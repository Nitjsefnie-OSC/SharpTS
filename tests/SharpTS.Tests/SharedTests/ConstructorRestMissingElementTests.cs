using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ConstructorRestMissingElementTests
{
    [Theory, ModeData]
    public void EmptyRestAfterDefaultParameterReadsUndefined(ExecutionMode mode)
    {
        const string source = """
            class C {
                constructor(value: number = 1, ...tail: any[]) {
                    console.log(tail.length, tail[0] === undefined, typeof tail[0], tail[0]);
                }
            }
            const Alias: any = C;
            new Alias();
            new Alias(1);
            new C();
            new C(1);
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("0 true undefined undefined\n", 4)),
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void MissingRestElementsDifferFromExplicitNullElements(ExecutionMode mode)
    {
        const string source = """
            class Base {
                constructor(...tail: any[]) {
                    console.log(tail.length, tail[0] === undefined, tail[0] === null,
                        tail[1] === undefined, typeof tail[1]);
                }
            }
            class Derived extends Base {}
            new Base();
            new Base(null);
            new Derived();
            new Derived(null);
            function read(...tail: any[]) {
                console.log(tail.length, tail[0] === undefined, tail[0] === null,
                    tail[1] === undefined, typeof tail[1]);
            }
            read();
            read(null);
            """;
        Assert.Equal("0 true false true undefined\n1 false true true undefined\n" +
            "0 true false true undefined\n1 false true true undefined\n" +
            "0 true false true undefined\n1 false true true undefined\n",
            TestHarness.Run(source, mode));
    }
}
