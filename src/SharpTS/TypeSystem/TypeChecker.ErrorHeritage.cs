using SharpTS.Parsing;
using SharpTS.Runtime.BuiltIns;

namespace SharpTS.TypeSystem;

public partial class TypeChecker
{
    private void CaptureDefaultLibraryErrorBindings(TypeEnvironment environment)
    {
        foreach (string name in BuiltInNames.ErrorTypeNames)
        {
            if (environment.GetValueBinding(name) is { } binding)
                _defaultLibraryErrorBindings[name] = binding;
        }
    }

    /// <summary>
    /// Loaded libraries describe native Error constructors as constructable interfaces.
    /// Adapt only their canonical value bindings to the class heritage representation,
    /// retaining the selected library's signatures and instance/static member surfaces.
    /// </summary>
    private TypeInfo ProjectDefaultLibraryErrorHeritage(Expr expression, TypeInfo type)
    {
        if (!_hasDefaultLibraries || expression is not Expr.Variable variable ||
            !_defaultLibraryErrorBindings.TryGetValue(variable.Name.Lexeme, out var binding) ||
            !ReferenceEquals(_environment.GetValueBinding(variable.Name.Lexeme), binding) ||
            RefreshCompletedInterfaceBinding(type) is not TypeInfo.Interface constructor ||
            GetConstructorSignatures(constructor) is not { Count: > 0 } signatures ||
            signatures.Any(signature => signature.IsGeneric))
        {
            return type;
        }

        // Refresh by declaration identity: a later library/global augmentation may have
        // completed the return interface after the constructor signature was collected.
        // Looking up its name here would incorrectly admit a local type shadow instead.
        TypeInfo.Interface? instance = null;
        foreach (var signature in signatures)
        {
            if (RefreshCompletedInterfaceBinding(signature.ReturnType) is not TypeInfo.Interface result ||
                (instance is not null && !ReferenceEquals(instance, result)))
            {
                return type;
            }
            instance = result;
        }

        var projected = new TypeInfo.MutableClass(variable.Name.Lexeme);
        var constructors = signatures.Select(ConstructorSignatureToFunction).ToList();
        projected.Methods["constructor"] = constructors.Count == 1
            ? constructors[0]
            : new TypeInfo.OverloadedFunction(constructors, constructors[^1]);
        projected.MethodAccess["constructor"] = AccessModifier.Public;

        CopyErrorHeritageMembers(instance!, projected, isStatic: false);
        CopyErrorHeritageMembers(constructor, projected, isStatic: true);
        return projected.Freeze();
    }

    private void CopyErrorHeritageMembers(
        TypeInfo.Interface source,
        TypeInfo.MutableClass target,
        bool isStatic)
    {
        HashSet<string> selected = new(StringComparer.Ordinal);
        HashSet<TypeInfo.Interface> visited = new(ReferenceEqualityComparer.Instance);

        void Copy(TypeInfo.Interface current)
        {
            current = (TypeInfo.Interface)RefreshCompletedInterfaceBinding(current);
            if (!visited.Add(current)) return;

            if (!isStatic)
            {
                target.StringIndexType ??= current.StringIndexType;
                target.NumberIndexType ??= current.NumberIndexType;
                target.SymbolIndexType ??= current.SymbolIndexType;
            }

            // Select the declaring facet before copying flags. Unioning inherited
            // optional/readonly names would apply stale flags to a nearer override.
            foreach (var member in current.Members)
            {
                if (!selected.Add(member.Key) || (!isStatic && member.Key == "constructor"))
                    continue;

                TypeInfo memberType = WithOptionalPropertyUndefined(
                    RefreshCompletedInterfaceBinding(member.Value),
                    current.OptionalMembers.Contains(member.Key));
                bool method = current.MethodMembers?.Contains(member.Key) == true;
                if (isStatic)
                {
                    if (method)
                    {
                        target.StaticMethods[member.Key] = memberType;
                        target.StaticMethodAccess[member.Key] = AccessModifier.Public;
                    }
                    else
                    {
                        target.StaticProperties[member.Key] = memberType;
                        target.StaticFieldAccess[member.Key] = AccessModifier.Public;
                    }
                    if (current.ReadonlyMembers?.Contains(member.Key) == true)
                        target.StaticReadonlyFields.Add(member.Key);
                }
                else
                {
                    if (method)
                    {
                        target.Methods[member.Key] = memberType;
                        target.MethodAccess[member.Key] = AccessModifier.Public;
                    }
                    else
                    {
                        target.FieldTypes[member.Key] = memberType;
                        target.FieldAccess[member.Key] = AccessModifier.Public;
                    }
                    if (current.ReadonlyMembers?.Contains(member.Key) == true)
                        target.ReadonlyFields.Add(member.Key);
                }
            }

            if (current.Extends is not null)
                foreach (var parent in current.Extends)
                    Copy(parent);
        }

        Copy(source);
    }
}
