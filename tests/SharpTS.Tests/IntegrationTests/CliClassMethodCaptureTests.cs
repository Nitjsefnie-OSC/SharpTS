using SharpTS.Execution;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.IntegrationTests;

public sealed class CliClassMethodCaptureTests
{
    public const string Source = """
        function make(value: number) {
            return class {
                async read() { await Promise.resolve(0); return () => value; }
                *values(next: number) { yield () => next; }
                async *asyncValues(next: number) {
                    await Promise.resolve(0); yield () => next;
                }
            };
        }
        async function run() {
            const First = make(7), Second = make(9);
            const first = new First(), second = new Second();
            const one = await first.read(), two = await second.read();
            const generator = first.values(11).next().value;
            const asyncGenerator = (await second.asyncValues(13).next()).value;
            console.log(one(), two(), generator(), asyncGenerator());
        }
        run().catch((error: any) => console.log(error.name, error.message));
        """;

    [Fact]
    public void ResolvedMemberCallsPreserveSuspendedMethodClosures()
    {
        var statements = new Parser(new Lexer(Source).ScanTokens()).ParseOrThrow();
        var typeMap = new TypeChecker().Check(statements);
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var interpreter = new Interpreter(output, errors);
        new VariableResolver(interpreter).Resolve(statements);

        interpreter.Interpret(statements, typeMap);

        Assert.Equal("7 9 11 13\n", output.ToString().Replace("\r\n", "\n"));
        Assert.Empty(errors.ToString());
        Assert.Null(interpreter.LastUncaughtError);
    }

    [Fact]
    public void CliMemberCallsPreserveSuspendedMethodClosures()
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var script = directory.CreateFile("main.ts", Source);
        var result = CliTestHelper.RunCli($"--no-tsconfig \"{script}\"", directory.Path);

        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Equal("7 9 11 13\n", result.StandardOutput);
        Assert.Empty(result.StandardError);
    }
}
