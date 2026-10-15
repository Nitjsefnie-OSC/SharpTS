using System.Reflection;
using System.Reflection.Emit;
using SharpTS.Compilation;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public sealed class NumericEqualityRepresentationTests
{
    [Theory]
    [InlineData("===", false)]
    [InlineData("!==", true)]
    [InlineData("==", false)]
    [InlineData("!=", true)]
    public void NativeNumberOperandsKeepUnboxedIeeeComparison(string operation, bool negated)
    {
        var assembly = CompileAndVerify($"function compare(left: number, right: number): boolean {{ return left {operation} right; }}");
        var method = StableNumericHotPathTests.FindFunction(assembly, "compare");
        Assert.All(method.GetParameters(), parameter => Assert.Equal(typeof(double), parameter.ParameterType));
        Assert.Equal(typeof(bool), method.ReturnType);
        var instructions = StableNumericHotPathTests.ReadInstructions(method).ToArray();
        Assert.Contains(instructions, instruction => instruction.OpCode == OpCodes.Ceq);
        Assert.DoesNotContain(instructions, instruction => instruction.OpCode == OpCodes.Box &&
            instruction.Operand is Type type && (type == typeof(double) || type == typeof(bool)));
        Assert.DoesNotContain(instructions, instruction => instruction.Operand is MethodBase
            { Name: "LooseEquals" or "Equals" or "ConvertToNumber" or "ToNumber" or "ToDouble" });

        var compare = method.CreateDelegate<Func<double, double, bool>>();
        Assert.Equal(!negated, compare(0d, -0d));
        Assert.Equal(negated, compare(double.NaN, double.NaN));
    }

    [Theory]
    [InlineData(true, "===", false)]
    [InlineData(false, "===", false)]
    [InlineData(true, "!==", true)]
    [InlineData(false, "!==", true)]
    public void MixedStrictComparisonUsesNumberTypeTestWithoutExtraBoxes(bool nativeLeft, string operation, bool negated)
    {
        string expression = nativeLeft ? $"native {operation} dynamic" : $"dynamic {operation} native";
        var assembly = CompileAndVerify($"function compare(dynamic: any, native: number): boolean {{ return {expression}; }}");
        var method = StableNumericHotPathTests.FindFunction(assembly, "compare");
        Assert.Equal(typeof(object), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(double), method.GetParameters()[1].ParameterType);
        var instructions = StableNumericHotPathTests.ReadInstructions(method).ToArray();
        Assert.Contains(instructions, instruction => instruction.OpCode == OpCodes.Isinst && instruction.Operand == typeof(double));
        Assert.Contains(instructions, instruction => instruction.OpCode == OpCodes.Unbox_Any && instruction.Operand == typeof(double));
        Assert.DoesNotContain(instructions, instruction => instruction.OpCode == OpCodes.Box &&
            instruction.Operand is Type type && (type == typeof(double) || type == typeof(bool)));
        Assert.DoesNotContain(instructions, instruction => instruction.Operand is MethodBase
            { Name: "ConvertToNumber" or "ToNumber" or "ToDouble" });

        var compare = method.CreateDelegate<Func<object?, double, bool>>();
        Assert.Equal(negated, compare(null, 0d));
        Assert.Equal(negated, compare("0", 0d));
        Assert.Equal(negated, compare(false, 0d));
        Assert.Equal(!negated, compare(0d, -0d));
        Assert.Equal(negated, compare(double.NaN, double.NaN));
    }

    [Theory]
    [InlineData("===", false, true)]
    [InlineData("!==", true, true)]
    [InlineData("==", false, false)]
    [InlineData("!=", true, false)]
    public void DefaultedNumberSlotKeepsRawValuesUntilEquality(string operation, bool negated, bool strict)
    {
        // Keep return adaptation out of this operand-representation assertion.
        var assembly = CompileAndVerify($"function compare(value: number = 1): any {{ return value {operation} 0; }}");
        var method = assembly.GetType("$Program")!
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Single(candidate => candidate.Name.EndsWith("compare", StringComparison.Ordinal) &&
                candidate.GetParameters().Length == 1);
        Assert.Equal(typeof(object), Assert.Single(method.GetParameters()).ParameterType);
        Assert.DoesNotContain(StableNumericHotPathTests.ReadInstructions(method), instruction =>
            instruction.Operand is MethodBase { Name: "ConvertToNumber" or "ToNumber" or "ToDouble" });

        var compare = method.CreateDelegate<Func<object?, object>>();
        Assert.Equal(negated, Assert.IsType<bool>(compare(null)));
        Assert.Equal(!negated, Assert.IsType<bool>(compare(0d)));
        if (strict) Assert.Equal(negated, Assert.IsType<bool>(compare("0")));
    }

    private static Assembly CompileAndVerify(string source)
    {
        var result = CompilationService.Compile(source);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        using var stream = new MemoryStream(result.AssemblyBytes!);
        using var verifier = new ILVerifier(extraProbeDirectories: [AppContext.BaseDirectory]);
        Assert.Empty(verifier.Verify(stream));
        var assembly = Assembly.Load(result.AssemblyBytes!);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name == "SharpTS");
        return assembly;
    }
}
