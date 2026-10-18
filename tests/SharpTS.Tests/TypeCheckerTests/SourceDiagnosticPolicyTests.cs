using SharpTS.Diagnostics;
using SharpTS.Modules;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class SourceDiagnosticPolicyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlobalAugmentationUsesItsOwnSourcePolicy(bool checkedAugmentation)
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "sharpts-policy-" + Guid.NewGuid().ToString("N"));
        var entryPath = Path.Combine(baseDirectory, "main.ts");
        var augmentationPath = Path.Combine(baseDirectory, "augmentation.ts");
        var files = new Dictionary<string, string>
        {
            [Path.Combine(baseDirectory, "base.ts")] = "enum Conflict { Value }",
            [augmentationPath] = (checkedAugmentation ? "" : "// @ts-nocheck\n") +
                "export {}; declare global { interface Conflict { value: number; } }",
            [entryPath] = (checkedAugmentation ? "// @ts-nocheck\n" : "") +
                "/// <reference path='./base.ts' />\nimport './augmentation';",
        };
        var resolver = new ModuleResolver(entryPath, files, TypeScriptProgramOptions.Default);
        var entry = resolver.LoadProgram(entryPath);
        var checker = new TypeChecker().WithSourceDiagnosticPolicy(checkJs: false);

        checker.CheckModules(resolver.GetModulesInOrder(entry), resolver);

        if (checkedAugmentation)
            Assert.Contains(checker.GetDiagnostics(), diagnostic => diagnostic.FilePath == augmentationPath &&
                diagnostic.Message.Contains("Cannot augment", StringComparison.Ordinal));
        else
            Assert.Empty(checker.GetDiagnostics());
    }

    [Fact]
    public void UncheckedErrorsDoNotConsumeBudgetOrLoseExportTypes()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "sharpts-policy-" + Guid.NewGuid().ToString("N"));
        var entryPath = Path.Combine(baseDirectory, "main.ts");
        var dependencyPath = Path.Combine(baseDirectory, "dependency.js");
        var files = new Dictionary<string, string>
        {
            [dependencyPath] = string.Concat(Enumerable.Repeat("Math.abs('wrong');\n", 12)) + "export const later = 7;",
            [entryPath] = "import { later } from './dependency.js';\nconst invalid: string = later;",
        };
        var resolver = new ModuleResolver(entryPath, files, TypeScriptProgramOptions.Default);
        var entry = resolver.LoadProgram(entryPath);
        var modules = resolver.GetModulesInOrder(entry);
        var checker = new TypeChecker(maxErrors: 1).WithSourceDiagnosticPolicy(checkJs: false);

        var typeMap = checker.CheckModules(modules, resolver);

        Assert.NotNull(typeMap);
        var diagnostic = Assert.Single(checker.GetDiagnostics());
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(entryPath, diagnostic.FilePath);
        var dependency = Assert.Single(modules, module => module.Path == dependencyPath);
        Assert.IsType<TypeInfo.NumberLiteral>(dependency.ExportedTypes["later"]);
    }

    [Fact]
    public void DirectCheckerStillReportsJavaScriptDiagnostics()
    {
        var entryPath = Path.Combine(Path.GetTempPath(), "sharpts-policy-" + Guid.NewGuid().ToString("N"), "main.js");
        var files = new Dictionary<string, string> { [entryPath] = "console.log(Math.abs('wrong'));" };
        var resolver = new ModuleResolver(entryPath, files, TypeScriptProgramOptions.Default);
        var entry = resolver.LoadProgram(entryPath);
        var checker = new TypeChecker();

        checker.CheckModules(resolver.GetModulesInOrder(entry), resolver);

        Assert.Contains(checker.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
