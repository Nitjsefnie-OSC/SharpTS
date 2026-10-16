using SharpTS.Parsing;
using SharpTS.Runtime.BuiltIns;
using SharpTS.TypeSystem.Exceptions;

namespace SharpTS.TypeSystem;

public partial class TypeChecker
{
    private BindingSymbol? _defaultLibraryPromiseBinding;
    private readonly Dictionary<ClassMetadataCore, TypeInfo.Promise> _defaultLibraryPromiseBases =
        new(ReferenceEqualityComparer.Instance);

    private void ResetDefaultLibraryPromiseHeritage()
    {
        _defaultLibraryPromiseBinding = null;
        _defaultLibraryPromiseBases.Clear();
    }

    /// <summary>
    /// Adapts only the canonical loaded Promise constructor's generic construct
    /// signatures. Binding their heritage arguments here retains the selected
    /// library's typed executor and members without making Promise globally Any.
    /// </summary>
    private TypeInfo.Class? ProjectDefaultLibraryPromiseHeritage(
        Expr expression, TypeInfo type, List<string>? arguments, List<TypeNode?>? argumentNodes)
    {
        if (!_hasDefaultLibraries || _defaultLibraryPromiseBinding is null ||
            expression is not Expr.Variable { Name.Lexeme: BuiltInNames.Promise } variable ||
            !ReferenceEquals(_environment.GetValueBinding(BuiltInNames.Promise), _defaultLibraryPromiseBinding) ||
            RefreshCompletedInterfaceBinding(type) is not TypeInfo.Interface constructor ||
            GetConstructorSignatures(constructor) is not { Count: > 0 } signatures ||
            _globalDeclarationEnvironment?.GetTypeBinding(BuiltInNames.Promise) is not TypeInfo.GenericInterface instance)
            return null;

        int count = arguments?.Count ?? 0;
        var candidates = signatures.Where(signature => signature.TypeParams is { Count: > 0 } parameters &&
            count >= parameters.TakeWhile(parameter => parameter.Default is null).Count() &&
            count <= parameters.Count).ToList();
        if (candidates.Count == 0)
            throw new TypeCheckException(
                "No base constructor has the specified number of type arguments.",
                variable.Name.Line, tsCode: "TS2508");

        List<TypeInfo> typeArguments = arguments is null ? [] : arguments
            .Select((_, index) => ResolveTypeArg(arguments, argumentNodes, index)).ToList();
        var projected = new TypeInfo.MutableClass(BuiltInNames.Promise);
        List<TypeInfo.Function> constructors = [];
        TypeInfo.Promise? promiseType = null;
        foreach (var signature in candidates)
        {
            // Reuse generic argument/default/constraint validation, but do not
            // expose a separate open generic constructor as the runtime base.
            var generic = new TypeInfo.MutableClass(BuiltInNames.Promise).FreezeGeneric(signature.TypeParams!);
            var instantiated = (TypeInfo.InstantiatedGeneric)InstantiateGenericClass(generic, typeArguments);
            var substitutions = GenericClassSubs(generic, instantiated.TypeArguments);
            if (SubstituteConstructorMember(signature.ReturnType, substitutions) is not TypeInfo.Promise result ||
                (promiseType is not null && !Equals(promiseType, result)))
                return null;
            promiseType = result;
            constructors.Add(CopyEditorSignatureMetadata(signature, new TypeInfo.Function(
                signature.ParamTypes.Select(parameter => SubstituteConstructorMember(parameter, substitutions)).ToList(),
                result, signature.RequiredParams, signature.HasRestParam, ParamNames: signature.ParamNames)));
        }

        // Promise<T> annotations have a dedicated native TypeInfo. The global
        // declaration facet supplies the actual selected/augmented member shape.
        if (instance.TypeParams.Count != 1) return null;
        CopyPromiseHeritageMembers(instance, projected, isStatic: false,
            new Dictionary<string, TypeInfo>(StringComparer.Ordinal)
            {
                [instance.TypeParams[0].Name] = promiseType!.ValueType
            });
        CopyPromiseHeritageMembers(constructor, projected, isStatic: true, []);
        projected.Methods["constructor"] = constructors.Count == 1
            ? constructors[0]
            : new TypeInfo.OverloadedFunction(constructors, constructors[^1]);
        projected.MethodAccess["constructor"] = AccessModifier.Public;

        var resultClass = projected.Freeze();
        _defaultLibraryPromiseBases.Add(resultClass.Core, promiseType!);
        return resultClass;
    }

    private void CopyPromiseHeritageMembers(
        TypeInfo source, TypeInfo.MutableClass target, bool isStatic,
        Dictionary<string, TypeInfo> substitutions)
    {
        HashSet<string> selected = new(StringComparer.Ordinal);
        HashSet<TypeInfo> visited = new(ReferenceEqualityComparer.Instance);
        TypeInfo Sub(TypeInfo type) => SubstituteConstructorMember(type, substitutions);

        void Copy(TypeInfo current)
        {
            current = RefreshCompletedInterfaceBinding(current);
            if (!visited.Add(current)) return;
            var members = current switch
            {
                TypeInfo.Interface @interface => @interface.Members,
                TypeInfo.GenericInterface generic => generic.Members,
                _ => null
            };
            if (members is null) return;
            var optional = current is TypeInfo.Interface i ? i.OptionalMembers : ((TypeInfo.GenericInterface)current).OptionalMembers;
            var readOnly = current is TypeInfo.Interface r ? r.ReadonlyMembers : ((TypeInfo.GenericInterface)current).ReadonlyMembers;
            var methods = current is TypeInfo.Interface m ? m.MethodMembers : ((TypeInfo.GenericInterface)current).MethodMembers;
            var parents = current is TypeInfo.Interface p ? p.Extends : ((TypeInfo.GenericInterface)current).Extends;
            if (!isStatic)
            {
                target.StringIndexType ??= current is TypeInfo.Interface si ? si.StringIndexType : ((TypeInfo.GenericInterface)current).StringIndexType;
                target.NumberIndexType ??= current is TypeInfo.Interface ni ? ni.NumberIndexType : ((TypeInfo.GenericInterface)current).NumberIndexType;
                target.SymbolIndexType ??= current is TypeInfo.Interface sy ? sy.SymbolIndexType : ((TypeInfo.GenericInterface)current).SymbolIndexType;
            }
            foreach (var (name, member) in members)
            {
                if (!selected.Add(name) || (!isStatic && name == "constructor")) continue;
                var memberType = WithOptionalPropertyUndefined(Sub(member), optional.Contains(name));
                if (isStatic)
                {
                    if (methods?.Contains(name) == true)
                    {
                        target.StaticMethods[name] = memberType;
                        target.StaticMethodAccess[name] = AccessModifier.Public;
                    }
                    else
                    {
                        target.StaticProperties[name] = memberType;
                        target.StaticFieldAccess[name] = AccessModifier.Public;
                    }
                    if (readOnly?.Contains(name) == true) target.StaticReadonlyFields.Add(name);
                }
                else
                {
                    if (methods?.Contains(name) == true)
                    {
                        target.Methods[name] = memberType;
                        target.MethodAccess[name] = AccessModifier.Public;
                    }
                    else
                    {
                        target.FieldTypes[name] = memberType;
                        target.FieldAccess[name] = AccessModifier.Public;
                    }
                    if (readOnly?.Contains(name) == true) target.ReadonlyFields.Add(name);
                }
            }
            foreach (var parent in parents ?? []) Copy(parent);
        }

        Copy(source);
        if (!isStatic)
        {
            if (target.StringIndexType is { } text) target.StringIndexType = Sub(text);
            if (target.NumberIndexType is { } number) target.NumberIndexType = Sub(number);
            if (target.SymbolIndexType is { } symbol) target.SymbolIndexType = Sub(symbol);
        }
    }

    private Dictionary<string, TypeInfo> ComposePromiseHeritageSubstitutions(
        TypeInfo type, Dictionary<string, TypeInfo> substitutions)
    {
        if (type is not TypeInfo.InstantiatedGeneric { GenericDefinition: TypeInfo.GenericClass generic } instantiated)
            return substitutions;
        Dictionary<string, TypeInfo> composed = new(StringComparer.Ordinal);
        for (int index = 0; index < generic.TypeParams.Count && index < instantiated.TypeArguments.Count; index++)
            composed[generic.TypeParams[index].Name] = SubstituteConstructorMember(instantiated.TypeArguments[index], substitutions);
        return composed;
    }

    private bool TryGetDefaultLibraryPromiseType(TypeInfo type, out TypeInfo.Promise promise)
    {
        if (type is TypeInfo.Instance instance) type = instance.ResolvedClassType;
        Dictionary<string, TypeInfo> substitutions = [];
        HashSet<TypeInfo> visited = new(ReferenceEqualityComparer.Instance);
        for (TypeInfo? current = type; current is not null && visited.Add(current); current = GetSuperclass(current))
        {
            substitutions = ComposePromiseHeritageSubstitutions(current, substitutions);
            var core = ClassInfoAccessor.Get(current, value => value.Core, value => value.Core);
            if (core is not null && _defaultLibraryPromiseBases.TryGetValue(core, out var native))
            {
                promise = (TypeInfo.Promise)SubstituteConstructorMember(native, substitutions);
                return true;
            }
        }
        promise = null!;
        return false;
    }

    private bool TryGetDefaultLibraryPromiseConstructor(
        TypeInfo type, Dictionary<string, TypeInfo> substitutions, out TypeInfo constructor)
    {
        HashSet<TypeInfo> visited = new(ReferenceEqualityComparer.Instance);
        for (TypeInfo? current = type; current is not null && visited.Add(current); current = GetSuperclass(current))
        {
            substitutions = ComposePromiseHeritageSubstitutions(current, substitutions);
            if (GetMethods(current)?.GetValueOrDefault("constructor") is not { } candidate) continue;
            var core = ClassInfoAccessor.Get(current, value => value.Core, value => value.Core);
            if (core is not null && _defaultLibraryPromiseBases.ContainsKey(core))
            {
                constructor = SubstituteConstructorMember(candidate, substitutions);
                return true;
            }
            break; // A user constructor owns its ordinary argument policy.
        }
        constructor = null!;
        return false;
    }
}
