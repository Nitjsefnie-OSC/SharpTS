using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NumberBoundFunctionNameTests
{
    public const string IssueSource = """
        const constructor: any = Number, bound: any = constructor.bind(null);
        console.log(JSON.stringify(bound.name));
        """;

    [Theory, ModeData]
    public void BindingNumberCapturesItsGuestName(ExecutionMode mode)
        => Assert.Equal("\"bound Number\"\n", TestHarness.Run(IssueSource, mode));

    [Theory, ModeData]
    public void BoundNumberNamesAreIndependentSnapshots(ExecutionMode mode)
    {
        const string source = """
            const constructor: any = Number, first: any = constructor.bind(null);
            Object.defineProperty(constructor, "name", { value: "Numeric", configurable: true });
            const second: any = constructor.bind(null), nested: any = second.bind(null);
            console.log(first.name, second.name, nested.name);
            Object.defineProperty(constructor, "name", { value: 42, configurable: true });
            console.log(JSON.stringify(constructor.bind(null).name));
            delete constructor.name;
            console.log(JSON.stringify(constructor.bind(null).name), first.name, second.name);
            """;
        Assert.Equal("bound Number bound Numeric bound bound Numeric\n\"bound \"\n\"bound \" bound Number bound Numeric\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void BindingReadsNumberNameGetterOnce(ExecutionMode mode)
    {
        const string source = """
            const constructor: any = Number, state = { reads: 0 };
            Object.defineProperty(constructor, "name", {
                get() { state.reads++; return "Snapshot"; }, configurable: true
            });
            const bound: any = constructor.bind(null);
            console.log(bound.name, bound.name, state.reads);
            Object.defineProperty(constructor, "name", { value: "Later", configurable: true });
            console.log(bound.name, constructor.bind(null).name, state.reads);
            """;
        Assert.Equal("bound Snapshot bound Snapshot 1\nbound Snapshot bound Later 1\n",
            TestHarness.Run(source, mode));
    }
}
