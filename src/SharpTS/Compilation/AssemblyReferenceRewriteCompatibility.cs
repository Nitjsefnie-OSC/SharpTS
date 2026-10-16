using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PEPacker;
using SharpTS.Compilation.Symbols;

namespace SharpTS.Compilation;

/// <summary>Preserves interleaved nested types through the pinned PE-Packer 1.0.7 rewriter.</summary>
/// <remarks>
/// PE-Packer copies NestedClass rows by enclosing type, although that table is sorted by nested
/// type RID. Its public API has no table hook. Temporarily giving all nested types one top-level
/// owner makes its copy order valid; the original hierarchy is restored before the image leaves
/// this helper. The temporary image is never loaded; this transform changes only parent cells.
/// Remove this compatibility step when the dependency copies NestedClass rows in nested RID order.
/// </remarks>
internal static class AssemblyReferenceRewriteCompatibility
{
    private readonly record struct NestedType(int Type, int Parent);

    internal static byte[] Rewrite(byte[] source, IReferenceAssemblyIndex referenceIndex,
        Func<string, ReferenceAction> referencePolicy)
    {
        using var originalPe = new PEReader(new MemoryStream(source, writable: false));
        var originalMetadata = originalPe.GetMetadataReader();
        var hierarchy = ReadHierarchy(originalMetadata);
        bool needsNormalization = hierarchy.Zip(hierarchy.Skip(1))
            .Any(pair => pair.First.Parent > pair.Second.Parent);

        byte[] input = source;
        if (needsNormalization)
        {
            input = (byte[])source.Clone();
            var commonOwner = MetadataTokens.TypeDefinitionHandle(hierarchy[0].Parent);
            while (!originalMetadata.GetTypeDefinition(commonOwner).GetDeclaringType().IsNil)
                commonOwner = originalMetadata.GetTypeDefinition(commonOwner).GetDeclaringType();
            PatchParents(input, originalPe, hierarchy, MetadataTokens.GetRowNumber(commonOwner));
        }

        using var inputStream = new MemoryStream(input, writable: false);
        using var rewriter = new AssemblyReferenceRewriter(inputStream, referenceIndex, referencePolicy);
        rewriter.Rewrite();
        using var output = new MemoryStream();
        rewriter.Save(output);
        byte[] image = output.ToArray();
        if (!needsNormalization) return image;

        using (var rewrittenPe = new PEReader(new MemoryStream(image, writable: false)))
        {
            VerifyDefinitionIdentity(originalMetadata, rewrittenPe.GetMetadataReader());
            PdbEmitter.VerifyMethodMappingPreserved(source, image);
            PatchParents(image, rewrittenPe, hierarchy, commonParent: null);
        }
        using var restoredPe = new PEReader(new MemoryStream(image, writable: false));
        if (!hierarchy.SequenceEqual(ReadHierarchy(restoredPe.GetMetadataReader())))
            throw new InvalidOperationException("Reference rewriting changed the nested type hierarchy.");
        return image;
    }

    private static NestedType[] ReadHierarchy(MetadataReader metadata)
    {
        var hierarchy = metadata.TypeDefinitions.Select(handle =>
                new NestedType(MetadataTokens.GetRowNumber(handle), MetadataTokens.GetRowNumber(
                    metadata.GetTypeDefinition(handle).GetDeclaringType())))
            .Where(type => type.Parent != 0).ToArray();
        if (hierarchy.Length != metadata.GetTableRowCount(TableIndex.NestedClass))
            throw new InvalidOperationException("Nested type metadata has inconsistent row counts.");
        return hierarchy;
    }

    private static void PatchParents(byte[] image, PEReader pe, NestedType[] hierarchy, int? commonParent)
    {
        var metadata = pe.GetMetadataReader();
        int rowSize = metadata.GetTableRowSize(TableIndex.NestedClass);
        if (metadata.GetTableRowCount(TableIndex.NestedClass) != hierarchy.Length || rowSize is not (4 or 8))
            throw new InvalidOperationException("Reference rewriting changed the NestedClass table layout.");
        int width = rowSize / 2;
        int tableOffset = checked(pe.PEHeaders.MetadataStartOffset + metadata.GetTableMetadataOffset(TableIndex.NestedClass));
        if (tableOffset < 0 || (long)tableOffset + (long)rowSize * hierarchy.Length > image.Length)
            throw new InvalidOperationException("NestedClass metadata lies outside the assembly image.");
        for (int row = 0; row < hierarchy.Length; row++)
        {
            var cells = image.AsSpan(tableOffset + row * rowSize, rowSize);
            int nestedRid = width == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(cells)
                : checked((int)BinaryPrimitives.ReadUInt32LittleEndian(cells));
            if (nestedRid != hierarchy[row].Type)
                throw new InvalidOperationException("Reference rewriting reordered nested type definitions.");
            int parent = commonParent ?? hierarchy[row].Parent;
            if (width == 2) BinaryPrimitives.WriteUInt16LittleEndian(cells[width..], checked((ushort)parent));
            else BinaryPrimitives.WriteUInt32LittleEndian(cells[width..], checked((uint)parent));
        }
    }

    private static void VerifyDefinitionIdentity(MetadataReader original, MetadataReader rewritten)
    {
        if (original.TypeDefinitions.Count != rewritten.TypeDefinitions.Count)
            throw new InvalidOperationException("Reference rewriting changed the type definition count.");
        foreach (var handle in original.TypeDefinitions)
        {
            var before = original.GetTypeDefinition(handle);
            var after = rewritten.GetTypeDefinition(handle);
            if (original.GetString(before.Name) != rewritten.GetString(after.Name)
                || original.GetString(before.Namespace) != rewritten.GetString(after.Namespace)
                || before.Attributes != after.Attributes
                || !before.GetMethods().SequenceEqual(after.GetMethods())
                || !before.GetFields().SequenceEqual(after.GetFields()))
                throw new InvalidOperationException("Reference rewriting changed type definition identity.");
        }
    }
}
