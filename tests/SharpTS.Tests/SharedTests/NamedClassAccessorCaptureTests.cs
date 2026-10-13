using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NamedClassAccessorCaptureTests
{
    [Theory, ModeData]
    public void NamedStaticGetterRetainsEnclosingParameter(ExecutionMode mode)
    {
        const string source = """
            function make(value: number) {
                return class Named { static get captured() { return value; } };
            }
            console.log(make(7).captured);
            """;
        Assert.Equal("7\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NamedAndAnonymousAccessorsKeepIndependentMutableCaptures(ExecutionMode mode)
    {
        const string source = """
            class Base {}
            function named(value: number) {
                return class Named extends Base {
                    static get captured() { return value; }
                    static set captured(next: number) { value = next; }
                    static get self() { return Named; }
                    get captured() { return value; }
                };
            }
            function anonymous(value: number) {
                return class {
                    static get captured() { return value; }
                    static set captured(next: number) { value = next; }
                    get captured() { return value; }
                };
            }
            const first: any = named(7), second: any = named(8), control: any = anonymous(10);
            console.log(first.captured, second.captured, control.captured);
            first.captured = 9;
            control.captured = 11;
            console.log(first.captured, second.captured, control.captured, first.self === first, second.self === second);
            console.log(new first().captured, new second().captured, new control().captured);
            """;
        Assert.Equal("7 8 10\n9 8 11 true true\n9 8 11\n", TestHarness.Run(source, mode));
    }
}
