using SharpTS.Runtime.Types;

namespace SharpTS.Execution;

public partial class Interpreter
{
    private sealed record ClassMemberDefinition(
        int Position, object Key, ISharpTSCallable Function, bool IsStatic, bool? IsGetter);

    private sealed class ClassMemberProperty
    {
        public ISharpTSCallable? Method;
        public SharpTSFunction? Getter;
        public SharpTSFunction? Setter;
    }

    /// <summary>
    /// Methods and accessors are stored separately in the AST. Replay their definitions in
    /// source order before freezing the class tables: a method replaces the whole accessor,
    /// while another accessor preserves the other half only when no method intervened (#2235).
    /// Computed keys have already been evaluated once by the enclosing class definition.
    /// </summary>
    private static Dictionary<(bool IsStatic, object Key), ClassMemberProperty> NormalizeClassMemberDefinitions(
        List<ClassMemberDefinition> definitions,
        Dictionary<string, ISharpTSCallable> methods,
        Dictionary<string, ISharpTSCallable> staticMethods,
        Dictionary<string, SharpTSFunction> getters,
        Dictionary<string, SharpTSFunction> setters,
        Dictionary<string, SharpTSFunction> staticGetters,
        Dictionary<string, SharpTSFunction> staticSetters)
    {
        Dictionary<(bool IsStatic, object Key), ClassMemberProperty> properties = [];
        foreach (var definition in definitions.OrderBy(member => member.Position))
        {
            var key = (definition.IsStatic, definition.Key);
            if (!properties.TryGetValue(key, out var property))
                properties[key] = property = new();
            if (definition.IsGetter is null)
            {
                property.Method = definition.Function;
                property.Getter = property.Setter = null;
            }
            else
            {
                property.Method = null;
                if (definition.IsGetter.Value) property.Getter = (SharpTSFunction)definition.Function;
                else property.Setter = (SharpTSFunction)definition.Function;
            }
        }

        foreach (var ((isStatic, key), property) in properties)
        {
            if (key is not string name) continue;
            var targetMethods = isStatic ? staticMethods : methods;
            var targetGetters = isStatic ? staticGetters : getters;
            var targetSetters = isStatic ? staticSetters : setters;
            targetMethods.Remove(name);
            targetGetters.Remove(name);
            targetSetters.Remove(name);
            if (property.Method is { } method) targetMethods[name] = method;
            if (property.Getter is { } getter) targetGetters[name] = getter;
            if (property.Setter is { } setter) targetSetters[name] = setter;
        }
        return properties;
    }

    private static void InstallClassSymbolMembers(SharpTSClass klass,
        Dictionary<(bool IsStatic, object Key), ClassMemberProperty> properties)
    {
        foreach (var ((isStatic, key), property) in properties)
        {
            if (key is not SharpTSSymbol symbol) continue;
            if (property.Method is { } method) klass.AddSymbolMethod(symbol, method, isStatic);
            if (property.Getter is { } getter) klass.AddSymbolAccessor(symbol, getter, isStatic, isGetter: true);
            if (property.Setter is { } setter) klass.AddSymbolAccessor(symbol, setter, isStatic, isGetter: false);
        }
    }
}
