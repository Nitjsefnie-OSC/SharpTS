using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PromiseConstructorCompletionTests
{
    [Theory, ModeData]
    public void SpeciesPreservesValidConstructorReplacement(ExecutionMode mode)
    {
        const string source = """
            const replacement: any = { value: 43 };
            class Returned extends Promise<any> {
                constructor(executor: any) { super(executor); return replacement; }
            }
            class Source extends Promise<any> {
                static get [Symbol.species]() { return Returned; }
            }
            const source: any = new Source(resolve => resolve(1));
            console.log(source.then() === replacement, Returned.resolve(1) === replacement);
            """;
        Assert.Equal("true true\n", TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData(false, ExecutionMode.Interpreted)]
    [InlineData(false, ExecutionMode.Compiled)]
    [InlineData(true, ExecutionMode.Interpreted)]
    [InlineData(true, ExecutionMode.Compiled)]
    public void SpeciesRejectsAbsentOrNoncallableCapabilityCallbacks(bool invokeWithNoncallable, ExecutionMode mode)
    {
        string source = """
            class Returned extends Promise<any> {
                constructor(executor: any) {
                    super(() => {});
            """ + (invokeWithNoncallable ? "executor(1, 2);" : "") + """
                    return ({} as any);
                }
            }
            class Source extends Promise<any> {
                static get [Symbol.species]() { return Returned; }
            }
            const source: any = new Source(resolve => resolve(1));
            try { source.then(); console.log("no error"); }
            catch (error) { console.log(error instanceof TypeError); }
            """;
        Assert.Equal("true\n", TestHarness.Run(source, mode));
    }
}
