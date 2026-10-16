namespace SharpTS.Runtime.Types;

/// <summary>Explicit Symbol definitions for ordinary function property maps.</summary>
internal static class FunctionSymbolProperties
{
    internal static void Define(
        ref Dictionary<SharpTSSymbol, object?>? properties,
        ref Dictionary<SharpTSSymbol, (ISharpTSCallable? Get, ISharpTSCallable? Set)>? accessors,
        SharpTSSymbol key,
        SharpTSPropertyDescriptor descriptor)
    {
        if (descriptor.HasGet || descriptor.HasSet)
        {
            (ISharpTSCallable? Get, ISharpTSCallable? Set) previous = default;
            accessors?.TryGetValue(key, out previous);
            properties?.Remove(key);
            accessors ??= [];
            accessors[key] = (
                descriptor.HasGet ? descriptor.Get : previous.Get,
                descriptor.HasSet ? descriptor.Set : previous.Set);
        }
        else if (descriptor.HasValue || descriptor.HasWritable)
        {
            object? value = descriptor.HasValue ? descriptor.Value
                : properties is not null && properties.TryGetValue(key, out var existing)
                    ? existing : SharpTSUndefined.Instance;
            accessors?.Remove(key);
            properties ??= [];
            properties[key] = value;
        }
    }
}
