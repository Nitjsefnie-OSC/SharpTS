using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace SharpTS.Tests.Infrastructure;

public sealed class TestHarnessVerificationTests
{
    [Fact]
    public void InvalidSavedMethodNeverReachesExecution()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.GetPath("invalid.dll");
        SaveMethod(path, il => il.Emit(OpCodes.Ldnull));
        bool executed = false;
        var marker = directory.GetPath("executed.txt");

        var (errors, output) = TestHarness.VerifySavedAssemblyAndRun(path, () =>
        {
            executed = true;
            File.WriteAllText(marker, "executed");
            return "should never run";
        });

        Assert.NotEmpty(errors);
        Assert.Empty(output);
        Assert.False(executed);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void MissingDependencyNeverReachesExecution()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        string dependencyName = "missing_verification_dependency_" + Guid.NewGuid().ToString("N");
        var dependency = new PersistedAssemblyBuilder(new AssemblyName(dependencyName), typeof(object).Assembly);
        var type = dependency.DefineDynamicModule("Main").DefineType("Dependency", TypeAttributes.Public);
        var method = type.DefineMethod("Value", MethodAttributes.Public | MethodAttributes.Static,
            typeof(int), Type.EmptyTypes);
        var dependencyIl = method.GetILGenerator();
        dependencyIl.Emit(OpCodes.Ldc_I4_1);
        dependencyIl.Emit(OpCodes.Ret);
        var dependencyMethod = type.CreateType()!.GetMethod("Value")!;
        var path = directory.GetPath("consumer.dll");
        // Keep the dependency only as metadata in memory; the verifier must
        // resolve references from its legitimate on-disk probe directories.
        SaveMethod(path, il => il.Emit(OpCodes.Call, dependencyMethod));
        bool executed = false;

        var (errors, output) = TestHarness.VerifySavedAssemblyAndRun(path, () =>
        {
            executed = true;
            return "should never run";
        });

        Assert.Contains(errors, error => error.Contains(dependencyName, StringComparison.Ordinal));
        Assert.Empty(output);
        Assert.False(executed);
    }

    [Fact]
    public void ValidSavedMethodReachesExecutionExactlyOnceAfterVerificationDisposesFile()
    {
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.GetPath("valid.dll");
        SaveMethod(path, il => il.Emit(OpCodes.Ldc_I4_1));
        int executions = 0;

        var (errors, output) = TestHarness.VerifySavedAssemblyAndRun(path, () =>
        {
            // Exclusive access also proves verification's file stream is gone.
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            executions++;
            return "verified";
        });

        Assert.Empty(errors);
        Assert.Equal("verified", output);
        Assert.Equal(1, executions);
    }

    [Fact]
    public void PublicCompileVerifyAndRunStillExecutesVerifiedSource()
    {
        var (errors, output) = TestHarness.CompileVerifyAndRun("console.log('verified');");
        Assert.Empty(errors);
        Assert.Equal("verified\n", output);
    }

    private static void SaveMethod(string path, Action<ILGenerator> emitValue)
    {
        var assembly = new PersistedAssemblyBuilder(
            new AssemblyName("verification_gate_" + Guid.NewGuid().ToString("N")), typeof(object).Assembly);
        var type = assembly.DefineDynamicModule("Main").DefineType("Program", TypeAttributes.Public);
        var method = type.DefineMethod("Value", MethodAttributes.Public | MethodAttributes.Static,
            typeof(int), Type.EmptyTypes);
        var il = method.GetILGenerator();
        emitValue(il);
        il.Emit(OpCodes.Ret);
        type.CreateType();
        // The invalid and missing-dependency PEs are saved for inspection only.
        // None of these fixtures is loaded into an execution context.
        assembly.Save(path);
    }
}
