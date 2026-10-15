using System.Reflection;
using System.Runtime.CompilerServices;
using SharpTS.Compilation;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public sealed class PrimitiveClassFieldGuestStorageTests
{
    [Theory]
    [InlineData("declaration")]
    [InlineData("generic")]
    [InlineData("inherited-generic")]
    public void NativeClrSettersClearRawGuestOverridesWithoutChangingTheTypedAbi(string shape)
    {
        string source = shape switch
        {
            "declaration" => "class Counter { value: number = 1; flag: boolean = true; }",
            "generic" => "class Counter<T> { value: number = 1; flag: boolean = true; }",
            _ => """
                class Base<T> { value: number = 1; flag: boolean = true; }
                class Counter<U> extends Base<U[]> {}
                """,
        };
        var assembly = CompileAndVerify(source);
        var type = Assert.Single(assembly.GetTypes(), candidate =>
            candidate.Name.Split('`')[0] == "Counter");
        if (type.IsGenericTypeDefinition) type = type.MakeGenericType(typeof(string));

        var number = type.GetProperty("Value")!;
        var flag = type.GetProperty("Flag")!;
        Assert.Equal(typeof(double), number.PropertyType);
        Assert.Equal(typeof(bool), flag.PropertyType);
        Assert.Equal(typeof(double), Assert.Single(number.SetMethod!.GetParameters()).ParameterType);
        Assert.Equal(typeof(bool), Assert.Single(flag.SetMethod!.GetParameters()).ParameterType);
        Assert.Contains(number.DeclaringType!.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType == typeof(double));
        Assert.Contains(flag.DeclaringType!.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType == typeof(bool));

        var constructor = Assert.Single(type.GetConstructors(), candidate => candidate.GetParameters().Length == 0);
        object first = constructor.Invoke([]);
        object second = constructor.Invoke([]);
        var get = type.GetMethod("GetProperty")!;
        var set = type.GetMethod("SetProperty")!;
        var fields = type.GetProperty("Fields")!;

        AssertNoStore(number.DeclaringType!, first);
        Assert.Equal(1d, get.Invoke(first, ["value"]));
        Assert.Equal(true, get.Invoke(first, ["flag"]));
        AssertNoStore(number.DeclaringType!, first);

        set.Invoke(first, ["value", null]);
        set.Invoke(first, ["flag", null]);
        Assert.Null(get.Invoke(first, ["value"]));
        Assert.Null(get.Invoke(first, ["flag"]));
        var raw = Assert.IsAssignableFrom<IDictionary<string, object?>>(fields.GetValue(first));
        Assert.Null(raw["value"]);
        Assert.Null(raw["flag"]);

        number.SetValue(first, 7d);
        flag.SetValue(first, false);
        Assert.Equal(7d, get.Invoke(first, ["value"]));
        Assert.Equal(false, get.Invoke(first, ["flag"]));
        var native = Assert.IsAssignableFrom<IDictionary<string, object?>>(fields.GetValue(first));
        Assert.Equal(7d, native["value"]);
        Assert.Equal(false, native["flag"]);

        object token = new();
        set.Invoke(first, ["value", token]);
        set.Invoke(first, ["flag", token]);
        number.SetValue(second, 3d);
        flag.SetValue(second, false);
        Assert.Same(token, get.Invoke(first, ["value"]));
        Assert.Same(token, get.Invoke(first, ["flag"]));
        Assert.Equal(3d, get.Invoke(second, ["value"]));
        Assert.Equal(false, get.Invoke(second, ["flag"]));
        AssertNoStore(number.DeclaringType!, second);

        number.SetValue(first, 9d);
        flag.SetValue(first, true);
        Assert.Equal(9d, get.Invoke(first, ["value"]));
        Assert.Equal(true, get.Invoke(first, ["flag"]));
    }

    private static void AssertNoStore(Type owner, object instance)
    {
        var field = owner.GetField("_fields", BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
        if (!field.IsStatic)
        {
            Assert.Null(field.GetValue(instance));
            return;
        }
        var stores = Assert.IsType<ConditionalWeakTable<object, Dictionary<string, object?>>>(field.GetValue(null));
        Assert.False(stores.TryGetValue(instance, out _));
    }

    private static Assembly CompileAndVerify(string source)
    {
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        var types = new TypeChecker().Check(statements);
        var compiler = new ILCompiler($"primitive_fields_{Guid.NewGuid():N}");
        compiler.Compile(statements, types, new DeadCodeAnalyzer(types).Analyze(statements));
        var bytes = compiler.SaveToBytes();
        using var stream = new MemoryStream(bytes);
        using var verifier = new ILVerifier(extraProbeDirectories: [AppContext.BaseDirectory]);
        Assert.Empty(verifier.Verify(stream));
        var assembly = Assembly.Load(bytes);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name == "SharpTS");
        return assembly;
    }
}
