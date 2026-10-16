using SharpTS.Runtime.BuiltIns;

namespace SharpTS.TypeSystem;

public partial class TypeChecker
{
    private TypeInfo NormalizeFunctionApparentReceiver(TypeInfo receiver)
    {
        if (receiver is TypeInfo.InstantiatedGeneric instantiated &&
            FlattenInstantiatedInterface(instantiated) is { } flattened)
            return flattened;
        return RefreshCompletedInterfaceBinding(receiver);
    }

    private bool HasFunctionApparentSurface(TypeInfo receiver)
    {
        receiver = NormalizeFunctionApparentReceiver(receiver);
        if (receiver is TypeInfo.Record record)
            return record.IsCallable || record.IsConstructable;
        if (receiver is not TypeInfo.Interface @interface)
            return false;

        var visited = new HashSet<TypeInfo.Interface>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<TypeInfo.Interface>();
        pending.Push(@interface);
        while (pending.TryPop(out var current))
        {
            current = (TypeInfo.Interface)RefreshCompletedInterfaceBinding(current);
            if (!visited.Add(current)) continue;
            if (current.IsCallable || current.IsConstructable) return true;
            foreach (var parent in current.Extends ?? [])
                pending.Push(parent);
        }
        return false;
    }

    private TypeInfo? ResolveNamedInterfaceMember(TypeInfo.Interface receiver, string name)
    {
        var visited = new HashSet<TypeInfo.Interface>(ReferenceEqualityComparer.Instance);
        return Visit(receiver);

        TypeInfo? Visit(TypeInfo.Interface current)
        {
            current = (TypeInfo.Interface)RefreshCompletedInterfaceBinding(current);
            if (!visited.Add(current)) return null;
            if (current.Members.TryGetValue(name, out var member))
                return WithOptionalPropertyUndefined(member, current.OptionalMembers.Contains(name));
            foreach (var parent in current.Extends ?? [])
                if (Visit(parent) is { } inherited) return inherited;
            return null;
        }
    }

    private TypeInfo? ResolveApparentFunctionMember(TypeInfo receiver, string name)
    {
        if (!HasFunctionApparentSurface(receiver)) return null;

        // Use the global type facet, not a nested Function declaration or the
        // constructor value. Resolve completion lazily so global augmentations apply.
        if (_globalDeclarationEnvironment?.GetTypeBinding("Function") is { } declaredFunction)
        {
            return RefreshCompletedInterfaceBinding(declaredFunction) is TypeInfo.Interface function
                ? ResolveNamedInterfaceMember(function, name)
                : null;
        }
        if (_hasDefaultLibraries) return null;

        // GetFunctionMemberType returns Any for unknown names. Only expose its
        // known fallback surface when no global Function declaration was loaded.
        return name is "call" or "apply" or "bind" or "length" or "name"
            ? BuiltInTypes.GetFunctionMemberType(name, receiver)
            : null;
    }

    private TypeInfo? ResolveFunctionShapeMember(TypeInfo receiver, string name)
    {
        receiver = NormalizeFunctionApparentReceiver(receiver);
        if (!HasFunctionApparentSurface(receiver)) return null;
        if (receiver is TypeInfo.Interface @interface &&
            ResolveNamedInterfaceMember(@interface, name) is { } declared)
            return declared;
        if (receiver is TypeInfo.Record record && record.Fields.TryGetValue(name, out var field))
            return WithOptionalPropertyUndefined(field, record.IsFieldOptional(name));
        // Bracket reads use a declared string index before the apparent Function
        // surface. Explicit named members still take priority over that index.
        if (StringIndexOf(receiver) is not null) return null;
        return ResolveApparentFunctionMember(receiver, name);
    }
}
