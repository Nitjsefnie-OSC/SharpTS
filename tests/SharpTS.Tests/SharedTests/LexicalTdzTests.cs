using SharpTS.Execution;
using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class LexicalTdzTests
{
    private const string ShadowSource = """
        function outer() {
            let value = 100;
            {
                const read = () => value;
                try { console.log(read()); } catch (error: any) { console.log(error.name); }
                let value = 42;
                console.log(read());
            }
            console.log(value);
        }
        outer();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["outer-shadow", ShadowSource, "ReferenceError\n42\n100\n"];
        yield return ["async-no-initializer", """
            let value = 100;
            async function run() {
                await Promise.resolve(0);
                {
                    const read = () => value;
                    try { read(); } catch (error: any) { console.log(error.name); }
                    let value: number;
                    console.log(typeof value);
                    value = 42;
                    console.log(read());
                }
                console.log(value);
            }
            run();
            """, "ReferenceError\nundefined\n42\n100\n"];
        yield return ["handler-owners", """
            let value = 100;
            try {
                const read = () => value;
                try { read(); } catch (error: any) { console.log(error.name); }
                let value = 1;
                console.log(read());
                throw 0;
            } catch (error) {
                const read = () => value;
                try { read(); } catch (error: any) { console.log(error.name); }
                const value = 2;
                console.log(read());
            } finally {
                const read = () => value;
                try { read(); } catch (error: any) { console.log(error.name); }
                let value = 3;
                console.log(read());
            }
            console.log(value);
            """, "ReferenceError\n1\nReferenceError\n2\nReferenceError\n3\n100\n"];
        yield return ["var-destructuring-control", """
            function run() {
                {
                    var { x = 2, ...rest } = { x: 3, y: 4 };
                    var [first, , ...tail] = [5, 6, 7];
                    console.log(x, rest.y, first, tail.join(','));
                }
                console.log(x, first);
            }
            run();
            """, "3 4 5 7\n3 5\n"];
        yield return ["parameter-default-before-body", """
            let value = 100;
            function run(initial: number = value) {
                const read = () => value;
                try { read(); } catch (error: any) { console.log(error.name); }
                let value = 42;
                console.log(initial, read());
            }
            run();
            """, "ReferenceError\n100 42\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        // Compiled async block ownership (#2584) and default capture (#2585) have independent defects.
        ((string)row[0] is "async-no-initializer" or "parameter-default-before-body"
            ? new[] { ExecutionMode.Interpreted }
            : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void LexicalOwnersReserveBindingsUntilInitialization(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        string output;
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, compiledOutput) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            output = compiledOutput;
        }
        else
        {
            output = TestHarness.Run(source, mode);
        }
        Assert.Equal(expected, output);
    }

    public static IEnumerable<object[]> ResolvedCases()
    {
        yield return [ShadowSource, "ReferenceError\n42\n100\n"];
        foreach (var row in CapturedLexicalAssignmentTests.SourceCases()) yield return row;
    }

    [Theory, MemberData(nameof(ResolvedCases))]
    public void ResolvedClosuresUseTheReservedInnerBinding(string source, string expected)
    {
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var typeMap = new TypeChecker().Check(statements);
        using var output = new StringWriter();
        using var interpreter = new Interpreter(stdout: output, stderr: TextWriter.Null);
        new VariableResolver(interpreter).Resolve(statements);
        interpreter.Interpret(statements, typeMap);
        Assert.Null(interpreter.LastUncaughtError);
        Assert.Equal(expected, output.ToString().Replace("\r\n", "\n"));
    }
}
