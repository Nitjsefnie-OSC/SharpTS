using SharpTS.LanguageServer.Project;
using SharpTS.Tests.LanguageServer;
using Xunit;

namespace SharpTS.Tests.LanguageServer.Project;

// These tests temporarily change cwd or the process-wide NuGet cache environment.
[CollectionDefinition("CsprojParser", DisableParallelization = true)]
public sealed class CsprojParserTestsCollection { }

/// <summary>
/// Unit tests for CsprojParser.
/// </summary>
[Collection("CsprojParser")]
public class CsprojParserTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Parse_MsbuildXmlNamespace_ResolvesHintPathAndCachedPackage(bool versionAttribute)
    {
        string tempDir = LspTestFixtures.CreateTempDirectory();
        string? previousCache = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        try
        {
            string localLibrary = Path.Combine(tempDir, "libs", "LocalLibrary.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(localLibrary)!);
            File.WriteAllBytes(localLibrary, []);
            string cache = Path.Combine(tempDir, "packages");
            string packageLibrary = Path.Combine(cache, "testpackage", "1.2.3", "lib", "net10.0", "PackageLibrary.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(packageLibrary)!);
            File.WriteAllBytes(packageLibrary, []);
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", cache);
            string packageReference = versionAttribute
                ? "<PackageReference Include=\"TestPackage\" Version=\"1.2.3\" />"
                : "<PackageReference Include=\"TestPackage\"><Version>1.2.3</Version></PackageReference>";
            string project = Path.Combine(tempDir, "references.csproj");
            File.WriteAllText(project, $"""
                <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <ItemGroup>
                    <Reference Include="LocalLibrary"><HintPath>libs\LocalLibrary.dll</HintPath></Reference>
                    {packageReference}
                  </ItemGroup>
                </Project>
                """);

            Assert.Equal(new[] { localLibrary, packageLibrary }, CsprojParser.Parse(project));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", previousCache);
            LspTestFixtures.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void Parse_EmptyCsproj_ReturnsEmptyList()
    {
        var path = LspTestFixtures.CreateTempFile(LspTestFixtures.EmptyCsproj);
        try
        {
            var references = CsprojParser.Parse(path);

            Assert.NotNull(references);
            Assert.Empty(references);
        }
        finally
        {
            LspTestFixtures.CleanupTempFile(path);
        }
    }

    [Fact]
    public void Parse_WithHintPath_ResolvesRelativePath()
    {
        var tempDir = LspTestFixtures.CreateTempDirectory();
        try
        {
            // Create the libs subdirectory and a fake DLL
            var libsDir = Path.Combine(tempDir, "libs");
            Directory.CreateDirectory(libsDir);
            var dllPath = Path.Combine(libsDir, "MyLibrary.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());

            // Create csproj file
            var csprojPath = Path.Combine(tempDir, "test.csproj");
            File.WriteAllText(csprojPath, LspTestFixtures.CsprojWithHintPath);

            var references = CsprojParser.Parse(csprojPath);

            Assert.Single(references);
            Assert.Equal(dllPath, references[0]);
        }
        finally
        {
            LspTestFixtures.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void Parse_VersionInAttribute_ParsesCorrectly()
    {
        var path = LspTestFixtures.CreateTempFile(LspTestFixtures.CsprojWithPackageReferenceAttribute);
        try
        {
            // This test mainly verifies the parser doesn't throw
            // Actual package resolution depends on NuGet cache
            var references = CsprojParser.Parse(path);

            Assert.NotNull(references);
            // May or may not have references depending on package cache
        }
        finally
        {
            LspTestFixtures.CleanupTempFile(path);
        }
    }

    [Fact]
    public void Parse_VersionInElement_ParsesCorrectly()
    {
        var path = LspTestFixtures.CreateTempFile(LspTestFixtures.CsprojWithPackageReferenceElement);
        try
        {
            // This test mainly verifies the parser doesn't throw
            var references = CsprojParser.Parse(path);

            Assert.NotNull(references);
        }
        finally
        {
            LspTestFixtures.CleanupTempFile(path);
        }
    }

    [Fact]
    public void Parse_MissingFile_ThrowsFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.csproj");

        Assert.Throws<FileNotFoundException>(() => CsprojParser.Parse(nonExistentPath));
    }

    [Fact]
    public void Parse_InvalidXml_ThrowsException()
    {
        var path = LspTestFixtures.CreateTempFile(LspTestFixtures.InvalidXml);
        try
        {
            Assert.ThrowsAny<Exception>(() => CsprojParser.Parse(path));
        }
        finally
        {
            LspTestFixtures.CleanupTempFile(path);
        }
    }

    [Fact]
    public void Parse_MissingHintPath_SkipsReference()
    {
        var path = LspTestFixtures.CreateTempFile(LspTestFixtures.CsprojWithReferenceNoHintPath);
        try
        {
            var references = CsprojParser.Parse(path);

            // Reference without HintPath should be skipped
            Assert.Empty(references);
        }
        finally
        {
            LspTestFixtures.CleanupTempFile(path);
        }
    }

    [Fact]
    public void Parse_NonexistentReferencedFile_SkipsReference()
    {
        var tempDir = LspTestFixtures.CreateTempDirectory();
        try
        {
            // Create csproj that references a non-existent DLL
            var csprojPath = Path.Combine(tempDir, "test.csproj");
            File.WriteAllText(csprojPath, LspTestFixtures.CsprojWithHintPath);
            // Don't create the libs/MyLibrary.dll

            var references = CsprojParser.Parse(csprojPath);

            // Non-existent file should be skipped
            Assert.Empty(references);
        }
        finally
        {
            LspTestFixtures.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void Parse_MixedReferences_ParsesBoth()
    {
        var tempDir = LspTestFixtures.CreateTempDirectory();
        try
        {
            // Create the libs subdirectory and a fake DLL
            var libsDir = Path.Combine(tempDir, "libs");
            Directory.CreateDirectory(libsDir);
            var dllPath = Path.Combine(libsDir, "LocalLib.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());

            // Create csproj file
            var csprojPath = Path.Combine(tempDir, "test.csproj");
            File.WriteAllText(csprojPath, LspTestFixtures.CsprojWithMixedReferences);

            var references = CsprojParser.Parse(csprojPath);

            // Should have at least the local reference
            Assert.Contains(references, r => r.EndsWith("LocalLib.dll"));
        }
        finally
        {
            LspTestFixtures.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void Parse_AbsolutePath_ReturnsAbsolutePaths()
    {
        var tempDir = LspTestFixtures.CreateTempDirectory();
        try
        {
            // Create the libs subdirectory and a fake DLL
            var libsDir = Path.Combine(tempDir, "libs");
            Directory.CreateDirectory(libsDir);
            var dllPath = Path.Combine(libsDir, "MyLibrary.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());

            // Create csproj file
            var csprojPath = Path.Combine(tempDir, "test.csproj");
            File.WriteAllText(csprojPath, LspTestFixtures.CsprojWithHintPath);

            var references = CsprojParser.Parse(csprojPath);

            Assert.Single(references);
            Assert.True(Path.IsPathRooted(references[0]), "Returned path should be absolute");
        }
        finally
        {
            LspTestFixtures.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void Parse_RelativeCsprojPath_ResolvesCorrectly()
    {
        var originalDir = Directory.GetCurrentDirectory();
        var tempDir = LspTestFixtures.CreateTempDirectory();
        try
        {
            // Create the libs subdirectory and a fake DLL
            var libsDir = Path.Combine(tempDir, "libs");
            Directory.CreateDirectory(libsDir);
            var dllPath = Path.Combine(libsDir, "MyLibrary.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());

            // Create csproj file
            var csprojPath = Path.Combine(tempDir, "test.csproj");
            File.WriteAllText(csprojPath, LspTestFixtures.CsprojWithHintPath);

            // Change to temp directory and use relative path
            Directory.SetCurrentDirectory(tempDir);

            var references = CsprojParser.Parse("test.csproj");

            Assert.Single(references);
            Assert.True(Path.IsPathRooted(references[0]));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            LspTestFixtures.CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void Parse_EmptyHintPath_SkipsReference()
    {
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                </PropertyGroup>
                <ItemGroup>
                    <Reference Include="MyLibrary">
                        <HintPath></HintPath>
                    </Reference>
                </ItemGroup>
            </Project>
            """;

        var path = LspTestFixtures.CreateTempFile(csproj);
        try
        {
            var references = CsprojParser.Parse(path);

            Assert.Empty(references);
        }
        finally
        {
            LspTestFixtures.CleanupTempFile(path);
        }
    }

    [Fact]
    public void Parse_PackageReferenceWithoutVersion_Skipped()
    {
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                </PropertyGroup>
                <ItemGroup>
                    <PackageReference Include="SomePackage" />
                </ItemGroup>
            </Project>
            """;

        var path = LspTestFixtures.CreateTempFile(csproj);
        try
        {
            var references = CsprojParser.Parse(path);

            // Should not throw, and package without version is skipped
            Assert.NotNull(references);
        }
        finally
        {
            LspTestFixtures.CleanupTempFile(path);
        }
    }

    [Fact]
    public void Parse_MultipleReferencesWithHintPath_ReturnsAll()
    {
        var tempDir = LspTestFixtures.CreateTempDirectory();
        try
        {
            var libsDir = Path.Combine(tempDir, "libs");
            Directory.CreateDirectory(libsDir);

            // Create multiple DLLs
            var dll1 = Path.Combine(libsDir, "Lib1.dll");
            var dll2 = Path.Combine(libsDir, "Lib2.dll");
            File.WriteAllBytes(dll1, Array.Empty<byte>());
            File.WriteAllBytes(dll2, Array.Empty<byte>());

            var csproj = """
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                    </PropertyGroup>
                    <ItemGroup>
                        <Reference Include="Lib1">
                            <HintPath>libs\Lib1.dll</HintPath>
                        </Reference>
                        <Reference Include="Lib2">
                            <HintPath>libs\Lib2.dll</HintPath>
                        </Reference>
                    </ItemGroup>
                </Project>
                """;

            var csprojPath = Path.Combine(tempDir, "test.csproj");
            File.WriteAllText(csprojPath, csproj);

            var references = CsprojParser.Parse(csprojPath);

            Assert.Equal(2, references.Count);
            Assert.Contains(references, r => r.EndsWith("Lib1.dll"));
            Assert.Contains(references, r => r.EndsWith("Lib2.dll"));
        }
        finally
        {
            LspTestFixtures.CleanupTempDirectory(tempDir);
        }
    }
}
