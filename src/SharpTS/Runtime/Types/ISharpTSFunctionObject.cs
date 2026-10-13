namespace SharpTS.Runtime.Types;

/// <summary>Own property storage for interpreter callable objects.</summary>
internal interface ISharpTSFunctionObject : ISharpTSCallable
{
    SharpTSObject OwnProperties { get; }
}

internal static class FunctionObjectProperties
{
    internal static SharpTSObject Create(string name, int length)
    {
        var properties = new SharpTSObject([]);
        properties.DefineProperty("length", new SharpTSPropertyDescriptor(
            (double)length, writable: false, enumerable: false, configurable: true));
        properties.DefineProperty("name", new SharpTSPropertyDescriptor(
            name, writable: false, enumerable: false, configurable: true));
        return properties;
    }
}
