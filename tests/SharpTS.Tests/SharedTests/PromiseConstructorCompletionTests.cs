using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PromiseConstructorCompletionTests
{
    [Fact]
    public void InterpretedSpeciesPreservesValidConstructorReplacement()
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
        // #2156 tracks the compiled static/species factory boundary. This
        // regression covers the interpreter's former unchecked Promise cast.
        Assert.Equal("true true\n", TestHarness.RunInterpreted(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterpretedSpeciesRejectsAbsentOrNoncallableCapabilityCallbacks(bool invokeWithNoncallable)
    {
        // The corresponding compiled capability validation is tracked in #2156.
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
        Assert.Equal("true\n", TestHarness.RunInterpreted(source));
    }
}
