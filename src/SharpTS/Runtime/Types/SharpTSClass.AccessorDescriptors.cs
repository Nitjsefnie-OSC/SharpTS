namespace SharpTS.Runtime.Types;

public partial class SharpTSClass
{
    private bool _staticAccessorsMaterialized;
    private SharpTSObject? _staticSymbolAccessorObject;

    private static SharpTSPropertyDescriptor AccessorDescriptor(ISharpTSCallable? getter, ISharpTSCallable? setter, string? name = null)
    {
        if (name != null)
        {
            NameAccessor(getter, $"get {name}");
            NameAccessor(setter, $"set {name}");
        }
        return new()
        {
            Get = getter, RawGet = getter, HasGet = true,
            Set = setter, RawSet = setter, HasSet = true,
            Enumerable = false, HasEnumerable = true,
            Configurable = true, HasConfigurable = true,
        };
    }

    private static void NameAccessor(ISharpTSCallable? accessor, string name)
    {
        if (accessor is SharpTSFunction function)
            function.DefineProperty("name", new SharpTSPropertyDescriptor(name,
                writable: false, enumerable: false, configurable: true) { HasValue = true });
    }

    internal IEnumerable<(string Name, SharpTSPropertyDescriptor Descriptor)> OwnInstanceAccessorDescriptors()
    {
        foreach (var name in _getters.Keys.Concat(_setters.Keys).Distinct(StringComparer.Ordinal))
            yield return (name, AccessorDescriptor(_getters.GetValueOrDefault(name), _setters.GetValueOrDefault(name), name));
    }

    internal IEnumerable<(SharpTSSymbol Key, SharpTSPropertyDescriptor Descriptor)> OwnInstanceSymbolAccessorDescriptors()
    {
        foreach (var key in (_symbolGetters?.Keys.AsEnumerable() ?? []).Concat(_symbolSetters?.Keys.AsEnumerable() ?? []).Distinct())
            yield return (key, OwnSymbolAccessorDescriptor(key, false)!);
    }

    private SharpTSPropertyDescriptor? OwnSymbolAccessorDescriptor(SharpTSSymbol symbol, bool isStatic)
    {
        var getters = isStatic ? _staticSymbolGetters : _symbolGetters;
        var setters = isStatic ? _staticSymbolSetters : _symbolSetters;
        if (getters?.ContainsKey(symbol) != true && setters?.ContainsKey(symbol) != true) return null;
        return AccessorDescriptor(getters?.GetValueOrDefault(symbol), setters?.GetValueOrDefault(symbol));
    }

    private void MaterializeStaticAccessors()
    {
        if (_staticAccessorsMaterialized) return;
        _staticAccessorsMaterialized = true;
        if (_staticGetters.Count == 0 && _staticSetters.Count == 0) return;
        _staticPropertyObject ??= new SharpTSObject(_staticProperties);
        foreach (var name in _staticGetters.Keys.Concat(_staticSetters.Keys).Distinct(StringComparer.Ordinal))
            if (_staticPropertyObject.GetOwnPropertyDescriptor(name) == null)
                _staticPropertyObject.DefineProperty(name, AccessorDescriptor(
                    _staticGetters.GetValueOrDefault(name), _staticSetters.GetValueOrDefault(name), name));

    }

    internal bool HasInstanceAccessorDeclaration(string name)
        => _getters.ContainsKey(name) || _setters.ContainsKey(name)
            || _prototype?.GetOwnPropertyDescriptor(name) is { HasGet: true } or { HasSet: true }
            || Superclass?.HasInstanceAccessorDeclaration(name) == true;

    internal SharpTSPropertyDescriptor? FindInstanceAccessorDescriptor(string name)
        => Prototype.GetOwnPropertyDescriptor(name) ?? Superclass?.FindInstanceAccessorDescriptor(name);

    internal bool HasInstanceAccessorDeclaration(SharpTSSymbol key)
        => _symbolGetters?.ContainsKey(key) == true || _symbolSetters?.ContainsKey(key) == true
            || _prototype?.GetOwnPropertyDescriptor(key) is { HasGet: true } or { HasSet: true }
            || Superclass?.HasInstanceAccessorDeclaration(key) == true;

    internal SharpTSPropertyDescriptor? FindInstanceAccessorDescriptor(SharpTSSymbol key)
        => FindInstanceSymbolPropertyDescriptor(key);

    internal SharpTSPropertyDescriptor? FindInstanceSymbolPropertyDescriptor(SharpTSSymbol key)
        => Prototype.GetOwnPropertyDescriptor(key) ?? Superclass?.FindInstanceSymbolPropertyDescriptor(key);

    internal SharpTSPropertyDescriptor? GetOwnStaticSymbolPropertyDescriptor(SharpTSSymbol symbol)
        => _staticSymbolAccessorObject?.GetOwnPropertyDescriptor(symbol)
            ?? (_staticSymbolProperties?.TryGetValue(symbol, out var value) == true
                ? new SharpTSPropertyDescriptor(value, writable: true, enumerable: true, configurable: true)
                    { HasValue = true } : null);

    internal SharpTSPropertyDescriptor? FindStaticSymbolAccessorDescriptor(SharpTSSymbol symbol)
        => _staticSymbolAccessorObject?.GetOwnPropertyDescriptor(symbol)
            ?? Superclass?.FindStaticSymbolAccessorDescriptor(symbol);

    internal bool DefineStaticSymbolProperty(SharpTSSymbol symbol, SharpTSPropertyDescriptor descriptor)
        => (_staticSymbolAccessorObject ??= new SharpTSObject([])).DefineProperty(symbol, descriptor);

    internal bool DeleteStaticSymbolProperty(SharpTSSymbol symbol, bool strictMode)
    {
        if (_staticSymbolAccessorObject?.DeleteBySymbolStrict(symbol, strictMode) == false) return false;
        _staticSymbolProperties?.Remove(symbol);
        return true;
    }
}
