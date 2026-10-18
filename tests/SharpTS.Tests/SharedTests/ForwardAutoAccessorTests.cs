using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ForwardAutoAccessorTests
{
    [Fact]
    public void ForwardAndCompletedShapesKeepAutoAccessorsOutOfInstanceFields()
    {
        const string source = """
            function read() { return Later; }
            class Later {
                accessor value: number = 1;
                readonly accessor fixed: number = 2;
                static accessor count: number = 3;
                static readonly accessor stable: number = 4;
            }
            """;
        var statements = TestHarness.ParseOrThrow(source);
        var types = new TypeChecker().Check(statements);
        var function = Assert.IsType<Stmt.Function>(statements[0]);
        var returned = Assert.IsType<Stmt.Return>(Assert.Single(function.Body!));
        var forward = Assert.IsType<TypeInfo.Class>(types.Get(returned.Value!));
        var completed = Assert.IsType<TypeInfo.Class>(types.GetClassType("Later"));

        Assert.Equal(completed.Core.DeclarationId, forward.Core.DeclarationId);
        foreach (var shape in new[] { forward, completed })
        {
            Assert.DoesNotContain("value", shape.FieldTypes.Keys);
            Assert.DoesNotContain("fixed", shape.FieldTypes.Keys);
            Assert.IsType<TypeInfo.Primitive>(shape.Getters["value"]);
            Assert.IsType<TypeInfo.Primitive>(shape.Setters["value"]);
            Assert.True(shape.Getters.ContainsKey("fixed"));
            Assert.False(shape.Setters.ContainsKey("fixed"));
            Assert.True(shape.StaticProperties.ContainsKey("count"));
            Assert.True(shape.Core.StaticSetterTypes.ContainsKey("count"));
            Assert.True(shape.StaticProperties.ContainsKey("stable"));
            Assert.False(shape.Core.StaticSetterTypes.ContainsKey("stable"));
            Assert.NotNull(shape.Core.StaticReadonlyFields);
            Assert.Contains("stable", shape.Core.StaticReadonlyFields.AsEnumerable());
            Assert.DoesNotContain("count", shape.Core.StaticReadonlyFields.AsEnumerable());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForwardReadonlyAutoAccessorWritesAreRejected(bool isStatic)
    {
        string receiver = isStatic ? "Later" : "new Later()";
        string modifier = isStatic ? "static " : "";
        string source = $$"""
            function write(): void { {{receiver}}.value = 2; }
            class Later { {{modifier}}readonly accessor value: number = 1; }
            """;
        var error = Assert.ThrowsAny<TypeCheckException>(() =>
            new TypeChecker().Check(TestHarness.ParseOrThrow(source)));
        Assert.Equal("TS2540", error.Diagnostic.TsCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForwardWritableAutoAccessorRejectsAnIncompatibleWrite(bool isStatic)
    {
        string receiver = isStatic ? "Later" : "new Later()";
        string modifier = isStatic ? "static " : "";
        string source = $$"""
            function write(): void { {{receiver}}.value = "wrong"; }
            class Later { {{modifier}}accessor value: number = 1; }
            """;
        var error = Assert.ThrowsAny<TypeCheckException>(() =>
            new TypeChecker().Check(TestHarness.ParseOrThrow(source)));
        Assert.Equal("TS2322", error.Diagnostic.TsCode);
    }

    [Theory, ModeData]
    public void ForwardWritableAutoAccessorsKeepTheirReadAndWriteTypes(ExecutionMode mode)
    {
        const string source = """
            function update(): number {
                const ctor = Later;
                const instance = new ctor();
                instance.value = 3;
                ctor.count = 4;
                return instance.value + ctor.count;
            }
            class Later {
                accessor value: number = 1;
                static accessor count: number = 2;
            }
            console.log(update());
            """;
        Assert.Equal("7\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AutoAccessorOverridesUseTheirStaticOrInstanceFacet(ExecutionMode mode)
    {
        const string source = """
            class Base {
                static accessor staticValue: string = "base-static";
                accessor value: string = "base-instance";
            }
            class Derived extends Base {
                static override accessor staticValue: string = "derived-static";
                override accessor value: string = "derived-instance";
            }
            const instance = new Derived();
            console.log(Base.staticValue, Derived.staticValue, new Base().value, instance.value);
            Derived.staticValue = "changed-static";
            instance.value = "changed-instance";
            console.log(Base.staticValue, Derived.staticValue, new Base().value, instance.value);
            """;
        Assert.Equal("base-static derived-static base-instance derived-instance\n" +
            "base-static changed-static base-instance changed-instance\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void StaticAutoAccessorOverridesNeedNoInstanceGetter()
    {
        const string source = """
            class Base { static accessor value: number = 1; }
            class Derived extends Base { static override accessor value: number = 2; }
            const value: number = Derived.value;
            """;
        new TypeChecker().Check(TestHarness.ParseOrThrow(source));
    }

    [Fact]
    public void SameNamedStaticAndInstanceAutoAccessorOverridesUseIndependentFacets()
    {
        const string source = """
            class Base {
                static accessor value: number = 1;
                accessor value: number = 2;
            }
            class Derived extends Base {
                static override accessor value: number = 3;
                override accessor value: number = 4;
            }
            """;
        new TypeChecker().Check(TestHarness.ParseOrThrow(source));
    }

    [Fact]
    public void AutoAccessorOverridesFollowTheirFacetThroughGenericAncestors()
    {
        const string source = """
            class Base<T> {
                static accessor value: number = 1;
                accessor value: number = 2;
            }
            class Middle extends Base<number> {}
            class Derived extends Middle {
                static override accessor value: number = 3;
                override accessor value: number = 4;
            }
            """;
        new TypeChecker().Check(TestHarness.ParseOrThrow(source));
    }

    [Theory]
    [InlineData("", "static ")]
    [InlineData("static ", "")]
    public void AutoAccessorOverrideRejectsAnAccessorOnTheWrongFacet(string baseModifier, string derivedModifier)
    {
        string source = $$"""
            class Base { {{baseModifier}}accessor value: number = 1; }
            class Derived extends Base { {{derivedModifier}}override accessor value: number = 2; }
            """;
        var error = Assert.ThrowsAny<TypeCheckException>(() =>
            new TypeChecker().Check(TestHarness.ParseOrThrow(source)));
        Assert.Equal("TS4113", error.Diagnostic.TsCode);
    }
}
