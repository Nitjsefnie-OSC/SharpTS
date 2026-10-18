using SharpTS.Runtime.BuiltIns;
using SharpTS.Runtime.Exceptions;

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
        foreach (var accessor in _instanceAutoAccessors)
        {
            string name = accessor.Name.Lexeme;
            var getter = BuiltInMethod.CreateV2($"get {name}", 0, (_, receiver, _) =>
                RuntimeValue.FromBoxed(GetOwnAutoAccessorStorage(receiver.ToObject(), name)[name]));
            var setter = accessor.IsReadonly ? null : BuiltInMethod.CreateV2($"set {name}", 1, (_, receiver, args) =>
            {
                GetOwnAutoAccessorStorage(receiver.ToObject(), name)[name] = args[0].ToObject();
                return RuntimeValue.Undefined;
            });
            yield return (name, AccessorDescriptor(getter, setter));
        }
    }

    private Dictionary<string, object?> GetOwnAutoAccessorStorage(object? receiver, string name)
    {
        if (receiver is null || !_autoAccessorStorage.TryGetValue(receiver, out var storage)
            || !storage.ContainsKey(name))
            throw new ThrowException(new SharpTSTypeError(
                $"Cannot access auto-accessor '{name}' on this receiver"));
        return storage;
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
                    { HasValue = true } : null)
            ?? (_staticSymbolMethods?.TryGetValue(symbol, out var method) == true
                ? new SharpTSPropertyDescriptor(method, writable: true, enumerable: false, configurable: true)
                    { HasValue = true } : null);

    internal SharpTSPropertyDescriptor? FindStaticSymbolAccessorDescriptor(SharpTSSymbol symbol)
        => GetOwnStaticSymbolPropertyDescriptor(symbol)
            ?? Superclass?.FindStaticSymbolAccessorDescriptor(symbol);

    internal bool DefineStaticSymbolProperty(SharpTSSymbol symbol, SharpTSPropertyDescriptor descriptor)
    {
        if (GetOwnStaticSymbolPropertyDescriptor(symbol) is null
            && !Compilation.PropertyDescriptorStore.IsExtensible(this))
            return false;
        if (!MaterializeOwnStaticProperty(symbol).DefineProperty(symbol, descriptor)) return false;
        RecordOwnStaticKey(symbol);
        return true;
    }

    internal bool DeleteStaticSymbolProperty(SharpTSSymbol symbol, bool strictMode)
    {
        if (_staticSymbolAccessorObject?.DeleteBySymbolStrict(symbol, strictMode) == false) return false;
        _staticSymbolProperties?.Remove(symbol);
        _staticSymbolMethods?.Remove(symbol);
        if (GetOwnStaticSymbolPropertyDescriptor(symbol) is null) _ownStaticPropertyOrder.Remove(symbol);
        return true;
    }
}
