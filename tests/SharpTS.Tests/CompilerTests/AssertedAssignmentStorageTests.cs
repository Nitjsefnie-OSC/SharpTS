using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public sealed class AssertedAssignmentStorageTests
{
    [Fact]
    public void AssertedNumericLocalKeepsTheAssignedValueAndReturn()
    {
        const string source = """
            function run(): number {
                let value: number = 1;
                const assigned = (value as any) = "stored";
                console.log(typeof value, value, assigned);
                return value;
            }
            const result = run();
            console.log(typeof result, result);
            """;
        AssertSaved(source, "string stored stored\nstring stored\n");
    }

    [Fact]
    public void AssertedPrimitiveParametersUseObjectStorage()
    {
        const string source = """
            function numberValue(value: number): number { (value as any) = "number"; return value; }
            function booleanValue(value: boolean): boolean { (value as any) = "boolean"; return value; }
            function stringValue(value: string) { (value as any) = 1; return value; }
            const numberResult = numberValue(1);
            const booleanResult = booleanValue(true);
            const stringResult = stringValue("before");
            console.log(typeof numberResult, numberResult);
            console.log(typeof booleanResult, booleanResult);
            console.log(typeof stringResult, stringResult);
            """;
        AssertSaved(source, "string number\nstring boolean\nnumber 1\n");
    }

    [Fact]
    public void AssertedCapturedParameterKeepsSharedStorageAndReturn()
    {
        const string source = """
            function run(value: number): number {
                const write = () => { (value as any) = "captured"; };
                write();
                console.log(typeof value, value);
                return value;
            }
            const result = run(1);
            console.log(typeof result, result);
            """;
        AssertSaved(source, "string captured\nstring captured\n");
    }

    [Fact]
    public void AssertedAwaitedAssignmentUsesTheAsyncStoragePath()
    {
        const string source = """
            async function run(value: number) {
                (value as any) = await Promise.resolve("awaited");
                console.log(typeof value, value);
            }
            run(1);
            """;
        AssertSaved(source, "string awaited\n");
    }

    [Fact]
    public void AssertedAssignmentKeepsGeneratorSuspensionAndResult()
    {
        const string source = """
            function* run() {
                let value: number = 1;
                (value as any) = yield "ready";
                console.log(typeof value, value);
                return value;
            }
            const iterator = run();
            console.log(iterator.next().value);
            console.log(iterator.next("resumed").value);
            """;
        AssertSaved(source, "ready\nstring resumed\nresumed\n");
    }

    [Fact]
    public void AssertedStringViewDoesNotCoerceACompoundRuntimeOperand()
    {
        const string source = """
            let value: any = 1;
            console.log((value as string) += 2, typeof value);
            """;
        AssertSaved(source, "3 number\n");
    }

    [Fact]
    public void AssertedConstTargetsThrowAtTheWriteAndKeepLogicalSkips()
    {
        const string source = """
            let trace = "";
            function rhs(tag: string): number { trace += tag; return 2; }
            const simple: number = 1;
            try { (simple as any) = rhs("s"); }
            catch (error) { console.log("simple", error instanceof TypeError, simple); }
            const compound: number = 1;
            try { (compound as any) += rhs("c"); }
            catch (error) { console.log("compound", error instanceof TypeError, compound); }
            const truthy: number = 1;
            const falsy: number = 0;
            console.log("skip", (truthy as any) ||= rhs("x"),
                (falsy as any) &&= rhs("x"), (truthy as any) ??= rhs("x"), trace);
            try { (truthy as any) &&= rhs("a"); }
            catch (error) { console.log("and", error instanceof TypeError, truthy); }
            try { (falsy as any) ||= rhs("o"); }
            catch (error) { console.log("or", error instanceof TypeError, falsy); }
            const missing: null = null;
            try { (missing as any) ??= rhs("n"); }
            catch (error) { console.log("nullish", error instanceof TypeError, missing); }
            console.log(trace);
            """;
        const string expected = "simple true 1\ncompound true 1\nskip 1 0 1 sc\nand true 1\nor true 0\nnullish true null\nscaon\n";
        Assert.Equal(expected, TestHarness.Run(source, ExecutionMode.Interpreted));
        AssertSaved(source, expected);
    }

    private static void AssertSaved(string source, string expected)
    {
        var (errors, output) = TestHarness.CompileVerifyAndRun(source);
        Assert.Empty(errors);
        Assert.Equal(expected, output);
    }
}
