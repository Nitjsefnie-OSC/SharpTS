using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GenericAutoAccessorStorageTests
{
    private const string Source = """
        class Box<T> {
            accessor value: T;
            constructor(value: T) { this.value = value; }
            read() { return this.value; }
            write(value: T) { return this.value = value; }
        }
        const numberBox = new Box<number>(42);
        const stringBox = new Box<string>("ok");
        console.log(numberBox.read(), stringBox.read());
        console.log(numberBox.write(43), stringBox.write("next"));
        const dynamicNumber: any = numberBox;
        const dynamicString: any = stringBox;
        dynamicNumber.value = 44;
        dynamicString.value = "last";
        console.log(numberBox.read(), stringBox.read());
        console.log(new Box<boolean>(true).read());
        """;

    [Theory, ModeData]
    public void GenericAutoAccessorWritesRetainValueAndReferenceTypes(ExecutionMode mode)
    {
        Assert.Equal("42 ok\n43 next\n44 last\ntrue\n", TestHarness.Run(Source, mode));
    }

    [Fact]
    public void GenericAutoAccessorAssemblyPassesCompleteVerification()
    {
        var (errors, output) = TestHarness.CompileVerifyAndRun(Source);
        Assert.Empty(errors);
        Assert.Equal("42 ok\n43 next\n44 last\ntrue\n", output);
    }

    [Theory]
    [InlineData("42", "number", "42\n")]
    [InlineData("\"ok\"", "string", "ok\n")]
    [InlineData("true", "boolean", "true\n")]
    public void GenericAutoAccessorInitializersPassCompleteVerification(string value, string type, string expected)
    {
        string source = $$"""
            class Box<T> {
                accessor value: T = {{value}} as any;
            }
            console.log(new Box<{{type}}>().value);
            """;
        Assert.Equal(expected, TestHarness.Run(source, ExecutionMode.Interpreted));
        var (errors, output) = TestHarness.CompileVerifyAndRun(source);
        Assert.Empty(errors);
        Assert.Equal(expected, output);
    }
}
