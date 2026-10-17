using SharpTS.Modules;
using SharpTS.Parsing;

namespace SharpTS.TypeSystem;

public partial class TypeChecker
{
    private sealed class ForwardClassDeclaration(
        Stmt.Class declaration, TypeEnvironment environment, int declarationId,
        ParsedModule? module, string? filePath, SourceDocument? standaloneSourceDocument)
    {
        public Stmt.Class Declaration { get; } = declaration;
        public TypeEnvironment Environment { get; } = environment;
        public TypeInfo.MutableClass Shape { get; } = new(declaration.Name.Lexeme, declarationId);
        public ParsedModule? Module { get; } = module;
        public string? FilePath { get; } = filePath;
        public SourceDocument? StandaloneSourceDocument { get; } = standaloneSourceDocument;
        public TypeInfo? Constructor { get; set; }
        public bool IsResolving { get; set; }
    }

    // The binding, rather than the spelling at the use site, owns the forward declaration. In
    // particular a parameter/local class named Later must not select an outer Later's constructor.
    private readonly Dictionary<BindingSymbol, ForwardClassDeclaration> _forwardClassDeclarations =
        new(ReferenceEqualityComparer.Instance);
    private int _forwardClassSignatureDepth;

    private BindingSymbol RegisterForwardClassDeclaration(Stmt.Class declaration)
    {
        BindingSymbol symbol = RegisterValueDeclaration(declaration.Name);
        if (!declaration.IsDeclare && (!_forwardClassDeclarations.TryGetValue(symbol, out var forward) ||
            !ReferenceEquals(forward.Environment, _environment)))
            _forwardClassDeclarations[symbol] = new(declaration, _environment, -symbol.Id,
                _currentModule, _filePath, _standaloneSourceDocument);
        return symbol;
    }

    private TypeInfo ResolveForwardClassConstructor(Token use, TypeInfo declared)
    {
        // Only deferred bodies (or an enclosing constructor signature being resolved for one)
        // need a forward value type. Direct reads and heritage retain their source-order checks.
        if (declared is not TypeInfo.Any ||
            _currentFunctionReturnType is null && _forwardClassSignatureDepth == 0 ||
            _environment.GetValueBinding(use.Lexeme) is not { } binding ||
            !_forwardClassDeclarations.TryGetValue(binding, out var forward))
            return declared;

        if (forward.Constructor is { } completed) return completed;
        if (forward.IsResolving) return forward.Shape.Freeze();

        forward.IsResolving = true;
        _forwardClassSignatureDepth++;
        ParsedModule? previousModule = _currentModule;
        string? previousFilePath = _filePath;
        SourceDocument? previousStandaloneSourceDocument = _standaloneSourceDocument;
        try
        {
            _currentModule = forward.Module;
            _filePath = forward.FilePath;
            _standaloneSourceDocument = forward.StandaloneSourceDocument;
            Stmt.Class declaration = forward.Declaration;
            var shape = forward.Shape;
            using var declarationScope = new EnvironmentScope(this, forward.Environment);
            // Reuse this shell in the authoritative class pass. Instances in an early signature
            // retain the declaration's identity and follow its final frozen instance shape.
            forward.Environment.DefineType(declaration.Name.Lexeme, shape);
            var (signatureEnvironment, typeParameters, superclass) = CreateClassTypeEnvironment(declaration);
            shape.Superclass = superclass;
            shape.IsAbstract = declaration.IsAbstract;
            RegisterSourceClassMembers(declaration, shape);
            signatureEnvironment.Define(declaration.Name.Lexeme, shape);
            signatureEnvironment.DefineType(declaration.Name.Lexeme, shape);
            signatureEnvironment.Define("this", new TypeInfo.Instance(shape));
            using (new EnvironmentScope(this, signatureEnvironment))
            {
                CollectForwardClassSignatures(declaration, shape);
            }
            shape.ResetFrozenCache();
            return forward.Constructor = typeParameters is { Count: > 0 }
                ? shape.FreezeGeneric(typeParameters)
                : shape.Freeze();
        }
        finally
        {
            _forwardClassSignatureDepth--;
            forward.IsResolving = false;
            _currentModule = previousModule;
            _filePath = previousFilePath;
            _standaloneSourceDocument = previousStandaloneSourceDocument;
        }
    }

    // Collect only declaration signatures. Decorators, computed-key expressions, default values,
    // initializers and bodies are checked once, at the declaration's normal source position.
    private void CollectForwardClassSignatures(Stmt.Class declaration, TypeInfo.MutableClass shape)
    {
        CollectClassIndexSignatures(declaration, shape);
        var methodGroups = declaration.Methods.Select(method => (
                Method: method,
                Name: method.ComputedKey is null ? method.Name.Lexeme : TryGetWellKnownSymbolMemberName(method.ComputedKey)))
            .Where(candidate => candidate.Name is not null)
            .GroupBy(candidate => (Name: candidate.Name!, candidate.Method.IsStatic, candidate.Method.IsPrivate));
        foreach (var group in methodGroups)
        {
            string name = group.Key.Name;
            var methodsInGroup = group.Select(candidate => candidate.Method).ToList();
            var method = methodsInGroup.FirstOrDefault(candidate => candidate.Body is not null) ?? methodsInGroup[0];
            var signature = BuildForwardClassMethodSignature(method);
            var methods = method.IsPrivate
                ? method.IsStatic ? shape.StaticPrivateMethods : shape.PrivateMethods
                : method.IsStatic ? shape.StaticMethods : shape.Methods;
            var overloads = methodsInGroup.Where(candidate => candidate.Body is null && !candidate.IsAbstract)
                .Select(BuildForwardClassMethodSignature).ToList();
            methods[name] = overloads.Count > 0 ? new TypeInfo.OverloadedFunction(overloads, signature) : signature;
            if (methods[name] is TypeInfo.OverloadedFunction overloaded)
                RegisterEditorPublicSignatures(overloaded, overloads);
            (method.IsStatic ? shape.StaticMethodAccess : shape.MethodAccess)[name] = method.Access;

            if (method.Name.Lexeme == "constructor")
                foreach (var parameter in method.Parameters.Where(parameter => parameter.IsParameterProperty))
                    shape.FieldTypes[parameter.Name.Lexeme] = ResolveAnnotation(parameter.Type, parameter.TypeAnnotationNode, parameter)
                        ?? TypeInfo.Any.Shared;
        }
        foreach (var field in declaration.Fields)
        {
            string? name = field.ComputedKey is null ? field.Name.Lexeme : TryGetWellKnownSymbolMemberName(field.ComputedKey);
            if (name is null) continue;
            TypeInfo type = ResolveAnnotation(field.TypeAnnotation, field.TypeAnnotationNode, field)
                ?? (field.Initializer is Expr.Literal literal ? WidenLiteralType(GetLiteralType(literal.Value)) : TypeInfo.Any.Shared);
            var fields = field.IsPrivate
                ? field.IsStatic ? shape.StaticPrivateFields : shape.PrivateFields
                : field.IsStatic ? shape.StaticProperties : shape.FieldTypes;
            fields[name] = type;
            (field.IsStatic ? shape.StaticFieldAccess : shape.FieldAccess)[name] = field.Access;
            if (field.IsReadonly) (field.IsStatic ? shape.StaticReadonlyFields : shape.ReadonlyFields).Add(name);
        }
        foreach (var accessor in declaration.Accessors ?? [])
        {
            string? name = accessor.ComputedKey is null ? accessor.Name.Lexeme : TryGetWellKnownSymbolMemberName(accessor.ComputedKey);
            if (name is null) continue;
            PublishAccessorReadAccess(shape, accessor, name);
            if (accessor.Kind.Type == TokenType.GET)
                (accessor.IsStatic ? shape.StaticGetters : shape.Getters)[name] =
                    ResolveAnnotation(accessor.ReturnType, accessor.ReturnTypeNode, accessor, EditorAnnotationSlot.Return) ?? TypeInfo.Any.Shared;
            else
                (accessor.IsStatic ? shape.StaticSetters : shape.Setters)[name] = accessor.SetterParam is { } parameter
                    ? ResolveAnnotation(parameter.Type, parameter.TypeAnnotationNode, parameter) ?? TypeInfo.Any.Shared
                    : TypeInfo.Any.Shared;
        }
        PublishStaticAccessorTypes(shape);
        foreach (var accessor in declaration.AutoAccessors ?? [])
        {
            string name = accessor.Name.Lexeme;
            TypeInfo type = ResolveAnnotation(accessor.TypeAnnotation, accessor.TypeAnnotationNode, accessor)
                ?? (accessor.Initializer is Expr.Literal literal ? WidenLiteralType(GetLiteralType(literal.Value)) : TypeInfo.Any.Shared);
            (accessor.IsStatic ? shape.StaticProperties : shape.FieldTypes)[name] = type;
            (accessor.IsStatic ? shape.StaticFieldAccess : shape.FieldAccess)[name] = accessor.Access;
        }
    }

    private TypeInfo.Function BuildForwardClassMethodSignature(Stmt.Function method)
    {
        var (parameterTypes, requiredParameters, hasRest, parameterNames) = BuildFunctionSignature(
            method.Parameters, validateDefaults: false, contextName: $"method '{method.Name.Lexeme}'",
            editorOwner: method, inspectDestructuredDefaults: false);
        TypeInfo result = ResolveAnnotation(method.ReturnType, method.ReturnTypeNode, method, EditorAnnotationSlot.Return)
            ?? TypeInfo.Any.Shared;
        if (method.IsGenerator && method.ReturnType is not null)
            result = method.IsAsync && result is not TypeInfo.AsyncGenerator ? new TypeInfo.AsyncGenerator(result)
                : !method.IsAsync && result is not TypeInfo.Generator ? new TypeInfo.Generator(result) : result;
        var signature = new TypeInfo.Function(parameterTypes, result,
            requiredParameters, hasRest,
            ResolveAnnotation(method.ThisType, method.ThisTypeNode, method, EditorAnnotationSlot.This),
            parameterNames);
        RegisterEditorSignature(signature, method, method.Name);
        return signature;
    }
}
