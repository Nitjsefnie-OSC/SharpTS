using SharpTS.Diagnostics;
using SharpTS.Modules;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class ModuleClassValueDeclarationOrderTests
{
    [Theory]
    [InlineData("new Later();", true)]
    [InlineData("new Later();", false)]
    [InlineData("class Earlier extends Later {}", true)]
    [InlineData("class Earlier extends Later {}", false)]
    public void ClassFromEarlierScriptCanBeUsedAtSmallerOffset(string use, bool retainDocuments)
    {
        var diagnostics = CheckSources(
            [new string(' ', 200) + "class Later {}", use], retainDocuments);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("new Later(); class Later {}", true)]
    [InlineData("new Later(); class Later {}", false)]
    [InlineData("class Earlier extends Later {} class Later {}", true)]
    [InlineData("class Earlier extends Later {} class Later {}", false)]
    public void SameScriptStillReportsEagerForwardClassUse(string source, bool retainDocuments)
    {
        var diagnostics = CheckSources(["class Ready {}", source], retainDocuments);

        var error = Assert.Single(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Equal("TS2449", error.TsCode);
        Assert.Equal("b.ts", Path.GetFileName(error.FilePath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ImportedClassAliasRetainsItsSourceOwnership(bool retainDocuments)
    {
        var diagnostics = CheckSources(
            [new string(' ', 200) + "export class Later {}",
                "import { Later as Renamed } from './a'; new Renamed(); class Earlier extends Renamed {}"],
            retainDocuments);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static IReadOnlyList<Diagnostic> CheckSources(string[] sources, bool retainDocuments)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"sharpts_class_order_{Guid.NewGuid():N}");
        string[] paths = [Path.Combine(directory, "a.ts"), Path.Combine(directory, "b.ts")];
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [paths[0]] = sources[0],
            [paths[1]] = sources[1],
        };
        var resolver = new ModuleResolver(paths[0], files);
        var modules = paths.Select(path => resolver.LoadModule(path)).ToList();
        if (!retainDocuments)
            foreach (var module in modules)
                module.Document = null;

        var checker = new TypeChecker();
        checker.CheckModules(modules, resolver);
        return checker.GetDiagnostics();
    }
}
