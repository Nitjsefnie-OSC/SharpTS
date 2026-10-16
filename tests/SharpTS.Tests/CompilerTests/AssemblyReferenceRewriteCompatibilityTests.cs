using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PEPacker;
using SharpTS.Compilation;
using SharpTS.Testing;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public sealed class AssemblyReferenceRewriteCompatibilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void InterleavedNestedOwnersKeepDefinitionRowsAndHierarchy(bool generic, bool deep)
    {
        byte[] source = CreateFixture(generic, deep: deep);
        byte[] unchanged = (byte[])source.Clone();
        byte[] rewritten = AssemblyReferenceRewriteCompatibility.Rewrite(source,
            EmbeddedReferenceAssemblyIndex.Default, ReferencePolicy.RetargetCoreLibOnly);
        Assert.Equal(unchanged, source);
        using var originalPe = new PEReader(new MemoryStream(source));
        using var finalPe = new PEReader(new MemoryStream(rewritten));
        var original = originalPe.GetMetadataReader();
        var final = finalPe.GetMetadataReader();
        Assert.Equal(original.TypeDefinitions.Count, final.TypeDefinitions.Count);
        Assert.Equal(original.MethodDefinitions.Count, final.MethodDefinitions.Count);
        var parents = original.TypeDefinitions.Select(handle =>
                MetadataTokens.GetRowNumber(original.GetTypeDefinition(handle).GetDeclaringType()))
            .Where(parent => parent != 0).ToArray();
        Assert.Contains(parents.Zip(parents.Skip(1)), pair => pair.First > pair.Second);
        foreach (var handle in original.TypeDefinitions)
        {
            var expected = original.GetTypeDefinition(handle);
            var actual = final.GetTypeDefinition(handle);
            Assert.Equal(original.GetString(expected.Name), final.GetString(actual.Name));
            Assert.Equal(expected.GetDeclaringType(), actual.GetDeclaringType());
            Assert.Equal(expected.GetMethods().ToArray(), actual.GetMethods().ToArray());
        }
        foreach (var handle in original.MethodDefinitions)
        {
            var expected = original.GetMethodDefinition(handle);
            var actual = final.GetMethodDefinition(handle);
            Assert.Equal(original.GetString(expected.Name), final.GetString(actual.Name));
            Assert.Equal(expected.GetDeclaringType(), actual.GetDeclaringType());
        }
        var references = final.AssemblyReferences.Select(handle =>
            final.GetString(final.GetAssemblyReference(handle).Name)).ToArray();
        Assert.Contains("System.Runtime", references);
        Assert.DoesNotContain("System.Private.CoreLib", references);
        using var verifier = new ILVerifier(extraProbeDirectories: [AppContext.BaseDirectory]);
        using var verifiedImage = new MemoryStream(rewritten, writable: false);
        Assert.Empty(verifier.Verify(verifiedImage));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyOrOrderedNestedTablesKeepTheirHierarchyAndInput(bool nested)
    {
        byte[] source = CreateFixture(generic: false, interleaved: false, nested: nested);
        byte[] unchanged = (byte[])source.Clone();
        byte[] rewritten = AssemblyReferenceRewriteCompatibility.Rewrite(source,
            EmbeddedReferenceAssemblyIndex.Default, ReferencePolicy.RetargetCoreLibOnly);
        Assert.Equal(unchanged, source);
        using var beforePe = new PEReader(new MemoryStream(source));
        using var afterPe = new PEReader(new MemoryStream(rewritten));
        var before = beforePe.GetMetadataReader();
        var after = afterPe.GetMetadataReader();
        Assert.Equal(nested ? 4 : 0, after.GetTableRowCount(TableIndex.NestedClass));
        foreach (var handle in before.TypeDefinitions)
            Assert.Equal(before.GetTypeDefinition(handle).GetDeclaringType(),
                after.GetTypeDefinition(handle).GetDeclaringType());
        using var verifier = new ILVerifier(extraProbeDirectories: [AppContext.BaseDirectory]);
        using var verifiedImage = new MemoryStream(rewritten, writable: false);
        Assert.Empty(verifier.Verify(verifiedImage));
    }

    private static byte[] CreateFixture(bool generic, bool deep = false, bool interleaved = true, bool nested = true)
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("InterleavedNestedTypes"), typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("InterleavedNestedTypes");
        var first = module.DefineType("First", TypeAttributes.Public);
        var second = module.DefineType("Second", TypeAttributes.Public);
        if (generic) { first.DefineGenericParameters("T"); second.DefineGenericParameters("U"); }
        if (nested)
        {
            DefineChild(first, "FirstOne", 1);
            if (interleaved) DefineChild(second, "SecondOne", 2);
            DefineChild(first, "FirstTwo", 3);
            if (!interleaved) DefineChild(second, "SecondOne", 2);
            DefineChild(second, "SecondTwo", 4);
        }
        first.CreateType();
        second.CreateType();
        using var stream = new MemoryStream();
        assembly.Save(stream);
        return stream.ToArray();

        void DefineChild(TypeBuilder owner, string name, int value)
        {
            var child = owner.DefineNestedType(name, TypeAttributes.NestedPublic);
            if (generic) child.DefineGenericParameters(owner == first ? "T" : "U");
            if (deep && name == "FirstOne")
            {
                var descendant = child.DefineNestedType("Deep", TypeAttributes.NestedPublic);
                if (generic) descendant.DefineGenericParameters("T");
                descendant.CreateType();
            }
            var method = child.DefineMethod("Value", MethodAttributes.Public | MethodAttributes.Static,
                typeof(int), Type.EmptyTypes);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4, value);
            il.Emit(OpCodes.Ret);
            child.CreateType();
        }
    }
}
