using SharpTS.Execution;
using SharpTS.Parsing;
using SharpTS.Tests.SharedTests;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.IntegrationTests;

public sealed class CliGeneratorClassCaptureTests
{
    [Theory]
    [MemberData(nameof(GeneratorClassEnclosingCaptureTests.Cases), MemberType = typeof(GeneratorClassEnclosingCaptureTests))]
    public void ResolvedGeneratorMethodsPreserveEnclosingCaptures(string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var typeMap = new TypeChecker().Check(statements);
        using var output = new StringWriter();
        using var errors = new StringWriter();
        using var interpreter = new Interpreter(output, errors);
        new VariableResolver(interpreter).Resolve(statements);

        interpreter.Interpret(statements, typeMap);

        Assert.Equal(expected, output.ToString().Replace("\r\n", "\n"));
        Assert.Empty(errors.ToString());
        Assert.Null(interpreter.LastUncaughtError);
    }

    [Theory]
    [MemberData(nameof(GeneratorClassEnclosingCaptureTests.Cases), MemberType = typeof(GeneratorClassEnclosingCaptureTests))]
    public void CliGeneratorMethodsPreserveEnclosingCaptures(string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        using var directory = CliTestHelper.CreateTempDirectory();
        var script = directory.CreateFile("main.ts", source);
        var result = CliTestHelper.RunCli($"--no-tsconfig \"{script}\"", directory.Path, TimeSpan.FromSeconds(30));

        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Equal(expected, result.StandardOutput);
        Assert.Empty(result.StandardError);
    }
}
