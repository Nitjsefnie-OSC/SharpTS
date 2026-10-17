using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NumberParseFloatAliasTests
{
    public const string IssueSource = """
        const constructor: any = Number;
        console.log(typeof constructor.parseFloat);
        console.log(constructor.parseFloat("2.5tail"));
        """;

    [Theory, ModeData]
    public void AliasedNumberExposesParseFloat(ExecutionMode mode)
        => Assert.Equal("function\n2.5\n", TestHarness.Run(IssueSource, mode));

    [Theory, ModeData]
    public void NumberAliasSharesGlobalParseFloatIdentityAndMetadata(ExecutionMode mode)
    {
        const string source = """
            const constructor: any = Number, parse: any = constructor.parseFloat;
            console.log(parse === parseFloat, parse === Number.parseFloat,
                parse === constructor["parseFloat"], parse === Reflect.get(constructor, "parseFloat"));
            const descriptor = Object.getOwnPropertyDescriptor(constructor, "parseFloat")!;
            console.log(Object.hasOwn(constructor, "parseFloat"), descriptor.value === parse,
                descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(parse.name, parse.length, parse("2.5tail"), parse.call({}, "3.5tail"));
            const bound: any = parse.bind(null, "4.5tail");
            console.log(bound.name, bound.length, bound());
            console.log(Number.isNaN(parse()), Number.isNaN(parse("invalid")));
            """;
        Assert.Equal("true true true true\ntrue true true false true\nparseFloat 1 2.5 3.5\nbound parseFloat 0 4.5\ntrue true\n",
            TestHarness.Run(source, mode));
    }
}
