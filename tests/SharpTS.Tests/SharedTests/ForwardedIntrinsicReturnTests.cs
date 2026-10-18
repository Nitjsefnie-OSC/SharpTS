using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ForwardedIntrinsicReturnTests
{
    public static IEnumerable<object[]> SharedCases()
    {
        yield return Case("parameter-shadow", """
            function raw(): number { return null as any; }
            function forward(parseInt: (value: string, radix: number) => number): number { return parseInt("7", 10); }
            console.log(forward(raw));
            """);
        yield return Case("local-shadow", """
            function forward(): number {
                const parseInt = (_value: string, _radix: number): number => null as any;
                return parseInt("7", 10);
            }
            console.log(forward());
            """);
        yield return ["import-shadow", new Dictionary<string, string>
        {
            ["mock.ts"] = "export function parseInt(_value: string, _radix: number): number { return null as any; }",
            ["main.ts"] = "import { parseInt } from './mock'; function forward(): number { return parseInt('7', 10); } console.log(forward());"
        }];
    }

    // Existing interpreter dispatch gaps: #2540 (Number/Math), #2541 (parseInt),
    // #2542 (string prototype). The includes cast also avoids checker gap #2543.
    // Their strict TypeScript/Node and fully verified saved baselines are recorded in
    // artifacts/p1-bugs/2299-followup-prep/baseline-controls; preserve correct compiler behavior.
    public static IEnumerable<object[]> CompiledCases()
    {
        yield return Case("number-parameter-shadow", """
            function forward(Number: { parseInt(value: string, radix: number): number }): number { return Number.parseInt("7", 10); }
            console.log(forward({ parseInt: () => null as any }));
            """);
        yield return Case("global-parseint-mutation", """
            const original = globalThis.parseInt;
            globalThis.parseInt = function (_text: string, _radix?: number): number { return null as any; };
            function forward(): number { return parseInt("7", 10); }
            console.log(forward()); globalThis.parseInt = original;
            """);
        yield return Case("number-member-mutation", """
            const original = Number.parseInt;
            Number.parseInt = function (_text: string, _radix?: number): number { return null as any; };
            function forward(): number { return Number.parseInt("7", 10); }
            console.log(forward()); Number.parseInt = original;
            """);
        yield return Case("math-parameter-shadow", """
            function forward(Math: { min(value: number): number }): number { return Math.min(7); }
            console.log(forward({ min: () => null as any }));
            """);
        yield return Case("math-member-mutation", """
            const original = Math.max;
            Math.max = function (..._values: number[]): number { return null as any; };
            function forward(): number { return Math.max(1, 2); }
            console.log(forward()); Math.max = original;
            """);
        yield return Case("string-indexof-prototype-mutation", """
            const original = String.prototype.indexOf;
            String.prototype.indexOf = function (_needle: string, _position?: number): number { return null as any; };
            function forward(input: string, needle: string, position: number): number { return input.indexOf(needle, position); }
            console.log(forward("abc", "b", 0)); String.prototype.indexOf = original;
            """);
        yield return Case("string-includes-prototype-mutation", """
            const original = (String.prototype as any).includes;
            (String.prototype as any).includes = function (_needle: string, _position?: number): boolean { return null as any; };
            function forward(input: string, needle: string, position: number): boolean { return input.includes(needle, position); }
            console.log(forward("abc", "b", 0)); (String.prototype as any).includes = original;
            """);
    }

    public static IEnumerable<object[]> SavedCases() => SharedCases().Concat(CompiledCases());
    public static IEnumerable<object[]> ModeCases() => SharedCases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ShadowedCallsPreserveRawReturns(string name, Dictionary<string, string> files, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal("null\n", TestHarness.RunModules(files, "main.ts", mode));
    }

    [Theory, MemberData(nameof(CompiledCases))]
    public void MutableIntrinsicsPreserveRawReturns(string name, Dictionary<string, string> files)
    {
        Assert.NotEmpty(name);
        Assert.Equal("null\n", TestHarness.RunModules(files, "main.ts", ExecutionMode.Compiled));
    }

    private static object[] Case(string name, string source) =>
        [name, new Dictionary<string, string> { ["main.ts"] = source }];
}
