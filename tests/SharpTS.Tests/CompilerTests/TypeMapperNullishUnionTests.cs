using System.Reflection;
using System.Reflection.Emit;
using SharpTS.Compilation;
using Xunit;
using TI = SharpTS.TypeSystem.TypeInfo;

namespace SharpTS.Tests.CompilerTests;

public sealed class TypeMapperNullishUnionTests
{
    private static TypeMapper NewMapper(bool generateUnions)
    {
        var module = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("nullish_mapper_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("main");
        var mapper = new TypeMapper(module);
        if (generateUnions)
            mapper.SetUnionGenerator(new UnionTypeGenerator(mapper));
        return mapper;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullishOnlyUnionsUseObjectWithoutGeneratedCarriers(bool generateUnions)
    {
        var mapper = NewMapper(generateUnions);
        TI.Union[] unions =
        [
            new([TI.Null.Shared, TI.Undefined.Shared]),
            new([TI.Undefined.Shared, TI.Null.Shared]),
            new([TI.Null.Shared, new TI.Union([TI.Undefined.Shared, TI.Null.Shared]), TI.Undefined.Shared]),
            new([TI.Null.Shared, TI.Null.Shared]),
            new([TI.Undefined.Shared, new TI.Union([TI.Undefined.Shared])]),
        ];

        foreach (var union in unions)
            Assert.Same(typeof(object), mapper.MapTypeInfoStrict(union));
        Assert.Empty(mapper.ModuleBuilder.GetTypes());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedNullablePrimitivesRetainStrictMappings(bool generateUnions)
    {
        var mapper = NewMapper(generateUnions);
        foreach (TI nullish in new TI[] { TI.Null.Shared, TI.Undefined.Shared })
        {
            Assert.Same(typeof(double?), mapper.MapTypeInfoStrict(new TI.Union([TI.Primitive.Number, nullish])));
            Assert.Same(typeof(bool?), mapper.MapTypeInfoStrict(new TI.Union([nullish, TI.Primitive.Boolean])));
            Assert.Same(typeof(string), mapper.MapTypeInfoStrict(new TI.Union([TI.String.Shared, nullish])));
        }
        Assert.Empty(mapper.ModuleBuilder.GetTypes());
    }
}
