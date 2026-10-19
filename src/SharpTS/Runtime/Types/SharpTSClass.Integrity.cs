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
        MaterializeOwnStaticProperty("name");
        MaterializeOwnStaticProperty("length");
        foreach (var name in _staticMethods.Keys) MaterializeOwnStaticProperty(name);

        // Even an empty store must become nonextensible: later definitions
        // must not allocate fresh extensible storage on a sealed constructor.
        _staticSymbolAccessorObject ??= new SharpTSObject([]);
        var symbols = new HashSet<SharpTSSymbol>();
        if (_staticSymbolProperties is not null) symbols.UnionWith(_staticSymbolProperties.Keys);
        if (_staticSymbolMethods is not null) symbols.UnionWith(_staticSymbolMethods.Keys);
        foreach (var symbol in symbols) MaterializeOwnStaticProperty(symbol);
    }
}
