namespace SharpTS.Runtime.Types;

public partial class SharpTSClass
{
    // Declarations may live only in method tables. Seed their complete own
    // descriptor before ordinary storage merges an incoming partial update.
    private SharpTSObject MaterializeOwnStaticProperty(string name)
    {
        var current = GetOwnStaticPropertyDescriptor(name);
        _staticPropertyObject ??= new SharpTSObject(_staticProperties);
        if (_staticPropertyObject.GetOwnPropertyDescriptor(name) is null && current is not null)
            _staticPropertyObject.DefineProperty(name, current);
        return _staticPropertyObject;
    }

    private SharpTSObject MaterializeOwnStaticProperty(SharpTSSymbol symbol)
    {
        var current = GetOwnStaticSymbolPropertyDescriptor(symbol);
        _staticSymbolAccessorObject ??= new SharpTSObject([]);
        if (_staticSymbolAccessorObject.GetOwnPropertyDescriptor(symbol) is null && current is not null)
            _staticSymbolAccessorObject.DefineProperty(symbol, current);
        return _staticSymbolAccessorObject;
    }
}
