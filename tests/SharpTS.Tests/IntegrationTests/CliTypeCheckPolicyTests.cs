using System.Diagnostics;
using SharpTS.Testing;
using Xunit;

namespace SharpTS.Tests.IntegrationTests;

public sealed class CliTypeCheckPolicyTests
{
    private const string SemanticError = "console.log(Math.abs('2'));";

    public static IEnumerable<object[]> UncheckedSources()
    {
        foreach (bool compiled in new[] { false, true })
        {
            yield return [compiled, "default.js", "", ""];
            yield return [compiled, "default.cjs", "", ""];
            yield return [compiled, "default.mjs", "", ""];
            yield return [compiled, "default.jsx", "", ""];
            yield return [compiled, "nocheck.ts", "// @ts-nocheck\n", ""];
            yield return [compiled, "nocheck.cjs", "// @ts-nocheck\n", "--checkJs"];
        }
    }

    [Theory, MemberData(nameof(UncheckedSources))]
    public void UncheckedSourceExecutes(bool compiled, string name, string pragma, string flags)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile(name, pragma + SemanticError);
        AssertExecution(directory, path, compiled, "2\n", "--no-tsconfig " + flags);
    }

    public static IEnumerable<object[]> CheckedSources()
    {
        foreach (bool compiled in new[] { false, true })
        {
            yield return [compiled, "checked.ts", "", ""];
            yield return [compiled, "checked.js", "// @ts-check\n", ""];
            yield return [compiled, "checked.cjs", "// @ts-check\n", ""];
            yield return [compiled, "checked.mjs", "// @ts-check\n", ""];
            yield return [compiled, "checked.js", "", "--checkJs"];
            yield return [compiled, "checked.cjs", "", "--checkJs"];
        }
    }

    [Theory, MemberData(nameof(CheckedSources))]
    public void CheckedSourceReportsSemanticError(bool compiled, string name, string pragma, string flags)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile(name, pragma + SemanticError);
        var result = Analyze(directory, path, compiled, "--no-tsconfig " + flags);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("expected type 'number'", result.StandardOutput + result.StandardError);
    }

    [Theory]
    [InlineData(false, "--checkJs", "")]
    [InlineData(true, "--checkJs", "")]
    [InlineData(false, "", "// @ts-check\n")]
    [InlineData(true, "", "// @ts-check\n")]
    public void CheckedCommonJsReportsImplicitAny(bool compiled, string flags, string pragma)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("strict.cjs", pragma + "function identity(value) { return value; }\nidentity(1);");
        var result = Analyze(directory, path, compiled, "--no-tsconfig --noImplicitAny " + flags);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("implicitly has an 'any' type", result.StandardOutput + result.StandardError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoCheckSuppressesCatchAnnotationDiagnostic(bool compiled)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("catch.ts", "// @ts-nocheck\ntry { throw 1; } catch (error: string) { console.log(error); }");
        AssertExecution(directory, path, compiled, "1\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncheckedSourceRetainsIllegalBreakDiagnostic(bool compiled)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("break.js", "break;");
        var result = Analyze(directory, path, compiled);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("'break' can only be used inside a loop or switch", result.StandardOutput + result.StandardError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckedImporterUsesUncheckedDependencyTypes(bool compiled)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        directory.CreateFile("unchecked.js", SemanticError + "\nexport function twice(value) { return value * 2; }");
        var path = directory.CreateFile("main.ts", """
            import { twice } from './unchecked.js';
            console.log(twice(3));
            """);
        AssertExecution(directory, path, compiled, "2\n6\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncheckedImporterDoesNotSuppressCheckedDependency(bool compiled)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        directory.CreateFile("checked.ts", SemanticError + "\nexport const value = 1;");
        var path = directory.CreateFile("main.js", "// @ts-nocheck\nimport { value } from './checked';\nconsole.log(value);");
        var result = Analyze(directory, path, compiled);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("checked.ts", result.StandardOutput + result.StandardError);
        Assert.Contains("expected type 'number'", result.StandardOutput + result.StandardError);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TsConfigCheckJsControlsDiagnostics(bool compiled, bool checkJs)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        directory.CreateFile("tsconfig.json", "{\"compilerOptions\":{\"allowJs\":true,\"checkJs\":" + checkJs.ToString().ToLowerInvariant() + "}}");
        var path = directory.CreateFile("main.cjs", SemanticError);
        if (checkJs)
        {
            var result = Analyze(directory, path, compiled, flags: "");
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("expected type 'number'", result.StandardOutput + result.StandardError);
        }
        else
        {
            AssertExecution(directory, path, compiled, "2\n", flags: "");
        }
    }

    [Theory]
    [InlineData(false, "broken.js", "const value = ;")]
    [InlineData(true, "broken.js", "const value = ;")]
    [InlineData(false, "nocheck.ts", "// @ts-nocheck\nconst value = ;")]
    [InlineData(true, "nocheck.ts", "// @ts-nocheck\nconst value = ;")]
    [InlineData(false, "early.js", "console.log(new.target);")]
    [InlineData(true, "early.js", "console.log(new.target);")]
    public void UncheckedSourceStillReportsSyntaxErrors(bool compiled, string name, string source)
    {
        using var directory = CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile(name, source);
        var result = Analyze(directory, path, compiled);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Parse Error", result.StandardOutput + result.StandardError);
    }

    private static CliTestHelper.CliResult Analyze(
        TempTestDirectory directory, string path, bool compiled, string flags = "--no-tsconfig") =>
        CliTestHelper.RunCli(compiled
                ? $"{flags} --compile \"{path}\" -o \"{directory.GetPath("output.dll")}\" --standalone --verify"
                : $"{flags} \"{path}\"",
            directory.Path, TimeSpan.FromSeconds(60));

    private static void AssertExecution(
        TempTestDirectory directory, string path, bool compiled, string expected, string flags = "--no-tsconfig")
    {
        var analysis = Analyze(directory, path, compiled, flags);
        Assert.True(analysis.ExitCode == 0, analysis.StandardOutput + analysis.StandardError);
        if (!compiled)
        {
            Assert.Equal(expected, analysis.StandardOutput);
            return;
        }

        Assert.Contains("IL verification passed.", analysis.StandardOutput);
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = directory.Path,
        };
        start.ArgumentList.Add(directory.GetPath("output.dll"));
        var execution = TestProcess.Run(start, CliTestHelper.DefaultTimeout);
        Assert.True(execution.ExitCode == 0, execution.StandardOutput + execution.StandardError);
        Assert.Empty(execution.StandardError);
        Assert.Equal(expected, execution.StandardOutput.Replace("\r\n", "\n"));
    }
}
