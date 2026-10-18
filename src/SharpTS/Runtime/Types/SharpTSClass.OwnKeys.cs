namespace SharpTS.Runtime.Types;

public partial class SharpTSClass
{
    private readonly List<object> _ownStaticPropertyOrder = [];
    private bool _staticPropertyOrderInitialized;

    internal void InitializeOwnStaticKeyOrder(IEnumerable<object> memberKeys)
    {
        _staticPropertyOrderInitialized = true;
        _ownStaticPropertyOrder.Clear();
        RecordOwnStaticKey("length");
        RecordOwnStaticKey("name");
        RecordOwnStaticKey("prototype");
        foreach (var key in memberKeys) RecordOwnStaticKey(key);
        // The legacy construction path may already contain initialized fields.
        // Deferred fields are recorded only when their initializer runs.
        foreach (var name in _staticProperties.Keys) RecordOwnStaticKey(name);
        foreach (var name in _staticAutoAccessorStorage.Keys) RecordOwnStaticKey(name);
    }

    private void EnsureOwnStaticKeyOrder()
    {
        if (_staticPropertyOrderInitialized) return;
        InitializeOwnStaticKeyOrder(_staticMethods.Keys.Cast<object>()
            .Concat(_staticGetters.Keys).Concat(_staticSetters.Keys));
    }

    private void RecordOwnStaticKey(object key)
    {
        EnsureOwnStaticKeyOrder();
        if (!_ownStaticPropertyOrder.Contains(key)) _ownStaticPropertyOrder.Add(key);
    }

    internal string[] OwnStaticStringKeys()
    {
        EnsureOwnStaticKeyOrder();
        List<(uint Index, string Name)> indices = [];
        List<string> names = [];
        foreach (var name in _ownStaticPropertyOrder.OfType<string>())
        {
            if (name is not ("name" or "length") && !HasOwnStaticMember(name)) continue;
            if (PropertySemantics.TryGetArrayIndex(name, out uint index)) indices.Add((index, name));
            else names.Add(name);
        }
        return indices.OrderBy(entry => entry.Index).Select(entry => entry.Name).Concat(names).ToArray();
    }

    internal SharpTSSymbol[] OwnStaticSymbolKeys()
    {
        EnsureOwnStaticKeyOrder();
        return _ownStaticPropertyOrder.OfType<SharpTSSymbol>()
            .Where(symbol => GetOwnStaticSymbolPropertyDescriptor(symbol) is not null).ToArray();
    }
}
