namespace SharpTS.Runtime.Types;

/// <summary>Ordinary descriptor-backed properties of the String, Number and Boolean constructors.</summary>
internal interface ISharpTSPrimitiveConstructor : ISharpTSMutableBuiltIn, ISharpTSSymbolPropertyBag
{
    bool HasOwnProperty(string name);
    bool DefineExtraProperty(string name, SharpTSPropertyDescriptor descriptor);
    SharpTSPropertyDescriptor? GetOwnPropertyDescriptor(string name);
    bool DeleteProperty(string name);
    bool DeletePropertyStrict(string name, bool strictMode);
    IEnumerable<string> OwnStringKeys();
    bool DefineProperty(SharpTSSymbol symbol, SharpTSPropertyDescriptor descriptor);
    SharpTSPropertyDescriptor? GetOwnPropertyDescriptor(SharpTSSymbol symbol);
    IEnumerable<SharpTSSymbol> GetSymbolPropertyNames();
    bool DeleteBySymbolStrict(SharpTSSymbol symbol, bool strictMode);
}
