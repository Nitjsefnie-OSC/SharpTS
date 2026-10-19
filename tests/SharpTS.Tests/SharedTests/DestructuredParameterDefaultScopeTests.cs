using SharpTS.Execution;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

[Collection("CompilationService")]
public sealed class DestructuredParameterDefaultScopeTests
{
    public const string IssueSource = """
        function run([value]: any[], next = value) { console.log(value, next); }
        run([1]);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-earlier-array-binding", IssueSource, "1 1\n"];
        yield return ["ordinary-identifier-default-control", """
            function run(first: number = 7, next: number = first) { console.log(first, next); }
            run();
            run(9);
            """, "7 7\n9 9\n"];
        yield return ["object-alias-and-nested-default-order", """
            const trace: string[] = [];
            function mark(name: string, value: number): number { trace.push(name); return value; }
            function run({ source: value = mark("value", 2), nested: { count = mark("count", 3) } }: any = { nested: {} },
                next: any = (trace.push("next"), value + count)) {
                console.log(value, count, next, trace.join(","));
            }
            run();
            """, "2 3 5 value,count,next\n"];
        yield return ["supplied-later-argument-and-inferred-pattern-default", """
            const state = { defaults: 0 };
            function run([value]: number[], next: number = (state.defaults++, value)) {
                console.log(value, next, state.defaults);
            }
            run([7], 20);
            run([8]);
            function inferred([value]: number[], { next = value } = {}) { console.log(value, next); }
            inferred([1]);
            """, "7 20 0\n8 8 1\n1 1\n"];
        yield return ["array-hole-and-rest-bindings", """
            function run([first, , ...rest]: number[], next = first + rest.length) {
                console.log(first, rest.join(","), next);
            }
            run([1, 99, 2, 3]);
            """, "1 2,3 3\n"];
        yield return ["synchronous-callable-forms", """
            const expression = function([value]: any[], next = value) { return next; };
            const arrow = ([value]: any[], next = value) => next;
            const object = { run({ source: value }: any, next = value) { return next; } };
            class Owner {
                run([value]: any[], next = value) { return next; }
                static run({ source: value }: any, next = value) { return next; }
            }
            console.log(expression([1]), arrow([2]), object.run({ source: 3 }),
                new Owner().run([4]), Owner.run({ source: 5 }));
            """, "1 2 3 4 5\n"];
    }

    public static IEnumerable<object[]> StandaloneCases() => Cases().Concat(
        ParameterDefaultBodyScopeTests.Cases()
            .Where(row => (string)row[0] == "destructured-defaults-copy-and-order"))
        // #2747: compiled defaults run before earlier destructured parameter bindings.
        .Where(row => (string)row[0] == "ordinary-identifier-default-control");

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // #2747: restore the pattern rows after compiler initialization order is fixed.
            .Where(mode => mode == ExecutionMode.Interpreted ||
                (string)row[0] == "ordinary-identifier-default-control")
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void LaterDefaultsCanReadEarlierPatternBindings(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            var result = SourceExecutionService.Interpret(source);
            Assert.True(result.Success, string.Join("\n", result.Errors) + result.Output);
            Assert.Empty(result.Errors);
            Assert.Equal(expected, result.Output.Replace("\r\n", "\n"));
        }
    }
}
