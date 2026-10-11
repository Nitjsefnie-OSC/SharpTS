using System.Security;
using System.Text.Json;
using SharpTS.DebuggerReferenceFixture;
using Xunit;

namespace SharpTS.Tests.DebugAdapter;

[Collection("DebugAdapterTests")]
public sealed class DapProjectLaunchTests
{
    [Theory]
    [InlineData("csproj")]
    [InlineData("references")]
    [InlineData("tsconfig")]
    [InlineData("directory")]
    public async Task ProjectAndExplicitReferencesLoadManagedAssemblyAndTypeScriptConfiguration(string context)
    {
        string directory = Path.Combine(Path.GetTempPath(), "sharpts-dap-project", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string program = Path.Combine(directory, "main.ts");
            await File.WriteAllTextAsync(program, """
                import { offset } from "@fixture/value";
                import { ReferenceProbe } from "dotnet:SharpTS.DebuggerReferenceFixture.ReferenceProbe";
                const result = ReferenceProbe.getAnswer() + offset;
                console.log(`managed=${result}`);
                """);
            await File.WriteAllTextAsync(Path.Combine(directory, "value.ts"), "export const offset = 1;");
            string tsconfig = Path.Combine(directory, "tsconfig.json");
            await File.WriteAllTextAsync(tsconfig, """
                { "compilerOptions": { "baseUrl": ".", "paths": { "@fixture/*": ["*"] } } }
                """);
            string assembly = typeof(ReferenceProbe).Assembly.Location;
            string project = Path.Combine(directory, "references.csproj");
            await File.WriteAllTextAsync(project, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <Reference Include="SharpTS.DebuggerReferenceFixture">
                      <HintPath>{SecurityElement.Escape(assembly)}</HintPath>
                    </Reference>
                  </ItemGroup>
                </Project>
                """);

            await using var dap = new DapProtocolHarness();
            AssertSuccess(await dap.RequestAsync("initialize", new { adapterID = "sharpts" }));
            AssertSuccess(await dap.RequestAsync("launch", new
            {
                program = "main.ts",
                cwd = directory,
                project = context switch
                {
                    "csproj" => "references.csproj",
                    "tsconfig" => "tsconfig.json",
                    "directory" => ".",
                    _ => null,
                },
                references = context == "csproj" ? Array.Empty<string>() : new[] { assembly },
            }));
            AssertSuccess(await dap.RequestAsync("configurationDone"));
            JsonElement output = await dap.WaitForEventAsync("output");
            Assert.Contains("managed=43", output.GetProperty("body").GetProperty("output").GetString());
            JsonElement exited = await dap.WaitForEventAsync("exited");
            Assert.Equal(0, exited.GetProperty("body").GetProperty("exitCode").GetInt32());
            await dap.WaitForEventAsync("terminated");
            AssertSuccess(await dap.RequestAsync("disconnect"));
            Assert.Equal(0, await dap.WaitForExitAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertSuccess(JsonElement response) =>
        Assert.True(response.GetProperty("success").GetBoolean(),
            response.TryGetProperty("message", out JsonElement message) ? message.GetString() : null);
}
