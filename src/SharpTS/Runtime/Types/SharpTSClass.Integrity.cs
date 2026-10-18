namespace SharpTS.Runtime.Types;

public partial class SharpTSClass
{
    internal void FreezeOwnStaticProperties()
    {
        MaterializeOwnStaticPropertiesForIntegrity();
        _staticPropertyObject!.Freeze();
        _staticSymbolAccessorObject!.Freeze();
    }

    internal void SealOwnStaticProperties()
    {
        MaterializeOwnStaticPropertiesForIntegrity();
        _staticPropertyObject!.Seal();
        _staticSymbolAccessorObject!.Seal();
    }

    private void MaterializeOwnStaticPropertiesForIntegrity()
    {
        MaterializeStaticAccessors();
        _staticPropertyObject ??= new SharpTSObject(_staticProperties);
        foreach (var (name, method) in _staticMethods)
        {
            if (!_deletedStaticMethods.Contains(name)
                && _staticPropertyObject.GetOwnPropertyDescriptor(name) is null)
                _staticPropertyObject.DefineProperty(name, SharpTSPropertyDescriptor.ForMethod(method));
        }

        // Even an empty store must become nonextensible: later definitions
        // must not allocate fresh extensible storage on a sealed constructor.
        _staticSymbolAccessorObject ??= new SharpTSObject([]);
        var symbols = new HashSet<SharpTSSymbol>();
        if (_staticSymbolProperties is not null) symbols.UnionWith(_staticSymbolProperties.Keys);
        if (_staticSymbolMethods is not null) symbols.UnionWith(_staticSymbolMethods.Keys);
        foreach (var symbol in symbols)
        {
            if (_staticSymbolAccessorObject.GetOwnPropertyDescriptor(symbol) is null
                && GetOwnStaticSymbolPropertyDescriptor(symbol) is { } descriptor)
                _staticSymbolAccessorObject.DefineProperty(symbol, descriptor);
        }
    }
}
