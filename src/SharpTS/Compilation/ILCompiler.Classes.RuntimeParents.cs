using System.Reflection;
using System.Reflection.Emit;
using SharpTS.Parsing;
using SharpTS.Parsing.Visitors;

namespace SharpTS.Compilation;

public partial class ILCompiler
{
    private readonly Dictionary<Stmt.Class, Expr.ClassExpr> _runtimeClassDeclarations = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Stmt.Class> _repeatingClassDeclarations = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<TypeBuilder, MethodBuilder> _receiverInitializers = new(ReferenceEqualityComparer.Instance);
    private bool _usesRuntimeParents;

    private void PrepareRepeatingClassDeclarations(IEnumerable<Stmt> statements)
    {
        var collector = new ClassDeclarationCollector();
        foreach (var statement in statements) collector.Visit(statement);
        foreach (var (declaration, owner) in collector.LexicalOwners)
            _classes.DeclarationLexicalOwners[declaration] = owner;
        foreach (var declaration in collector.RepeatingDeclarations)
        {
            var leaf = Expr.GetSuperclassLeafName(declaration.SuperclassExpr);
            if (declaration.IsDeclare || declaration.Decorators is { Count: > 0 }
                || declaration.AutoAccessors is { Count: > 0 }
                || declaration.Methods.Any(method => method.Decorators is { Count: > 0 }
                    || method.Parameters.Any(parameter => parameter.Decorators is { Count: > 0 }))
                || declaration.Fields.Any(field => field.Decorators is { Count: > 0 })
                || (declaration.Accessors?.Any(accessor => accessor.Decorators is { Count: > 0 }) ?? false)
                || Runtime.BuiltIns.BuiltInNames.IsErrorTypeName(leaf ?? "")
                || leaf is "Array" or "Promise" or "Map" or "Set" or "WeakMap" or "WeakSet" or "Date" or "RegExp" or "Buffer")
                continue;
            _repeatingClassDeclarations.Add(declaration);
            _runtimeClassDeclarations.TryAdd(declaration, CreateRuntimeClassExpression(declaration));
        }
    }

    private static Expr.ClassExpr CreateRuntimeClassExpression(Stmt.Class declaration) =>
        new(declaration.Name, declaration.TypeParams,
            declaration.SuperclassExpr, declaration.SuperclassTypeArgs, declaration.Methods,
            declaration.Fields, declaration.Accessors, declaration.AutoAccessors, declaration.Interfaces,
            declaration.InterfaceTypeArgs, declaration.IsAbstract, declaration.StaticInitializers);

    private bool HasLexicalClassExpressionOwner(Stmt.Class declaration)
    {
        object? owner = _classes.DeclarationLexicalOwners.GetValueOrDefault(declaration);
        while (owner is Stmt.Class parent)
            owner = _classes.DeclarationLexicalOwners.GetValueOrDefault(parent);
        return owner is Expr.ClassExpr;
    }

    private bool TryDefineRuntimeClassDeclaration(Stmt.Class declaration)
    {
        bool repeating = _repeatingClassDeclarations.Contains(declaration);
        var leaf = Expr.GetSuperclassLeafName(declaration.SuperclassExpr);
        bool expressionOwnedParent = ResolveClassExpressionParent(
            _typeMap.GetClassType(declaration)?.Superclass, declaration.SuperclassExpr) != null;
        if (expressionOwnedParent && declaration.Decorators is { Count: > 0 }) return false;
        // Declaration decorators and unsupported intrinsic heritage keep their
        // existing declaration handling; the expression adapter has no class
        // decorator metadata and must not relax its subclassing diagnostics.
        bool captureLexicalOwner = HasLexicalClassExpressionOwner(declaration)
            && declaration.Decorators is not { Count: > 0 }
            && leaf is not ("Map" or "Set" or "WeakMap" or "WeakSet" or "Date" or "RegExp" or "Buffer");
        if ((declaration.SuperclassExpr == null && !captureLexicalOwner && !repeating) || declaration.IsDeclare) return false;
        if (!captureLexicalOwner && !repeating && leaf != null && ((_typeMap.Get(declaration.SuperclassExpr!) is not TypeSystem.TypeInfo.Any
            && _classes.Builders.ContainsKey(GetDefinitionContext().ResolveClassName(leaf)) && !expressionOwnedParent)
            || Runtime.BuiltIns.BuiltInNames.IsErrorTypeName(leaf)
            || leaf is "Array" or "Promise" or "Map" or "Set" or "WeakMap" or "WeakSet" or "Date" or "RegExp" or "Buffer")) return false;
        if (!captureLexicalOwner && !repeating && declaration.SuperclassExpr is Expr.ClassExpr { Methods.Count: 0, Fields.Count: 0 }) return false;
        if (!_runtimeClassDeclarations.TryGetValue(declaration, out var expression))
            _runtimeClassDeclarations.Add(declaration, expression = CreateRuntimeClassExpression(declaration));
        if (_typeMap.GetClassType(declaration) is { } checkedType) _typeMap.SetClassExprType(expression, checkedType);
        CollectClassExpression(expression);
        _classExprs.Names[expression] = GetDefinitionContext().GetQualifiedClassName(_classExprs.Names[expression]);
        if (_classes.DeclarationLexicalOwners.TryGetValue(declaration, out var lexicalOwner))
        {
            string ownerName;
            if (lexicalOwner is Expr.ClassExpr parentExpression)
            {
                CollectClassExpression(parentExpression);
                ownerName = _classExprs.Names[parentExpression];
            }
            else ownerName = GetQualifiedClassDeclarationName((Stmt.Class)lexicalOwner);
            _classExprs.EnclosingClass[expression] = ownerName;
            _classes.LexicalEnclosingClasses[_classExprs.Names[expression]] = ownerName;
        }
        DefineClassExpression(expression);
        _usesRuntimeParents |= declaration.SuperclassExpr != null && (!repeating || HasRuntimeParent(expression));
        _classes.BlockScopedBuilders[declaration] = _classExprs.Builders[expression];
        _classes.DeclarationNames[declaration] = _classExprs.Names[expression];
        _classes.DeclarationNamespaces[declaration] = _currentNamespacePath;
        if (_typeMap.GetClassType(declaration) is { } declaredType)
        {
            _typeMapper.RegisterCheckedClassName(declaredType.Core.DeclarationId, _classExprs.Names[expression]);
            _typeMap.SetClassType(_classExprs.Names[expression], declaredType);
        }
        return true;
    }

    private bool HasRuntimeParent(Expr.ClassExpr expression)
    {
        // A CLR subclass can inherit a template whose guest parent is selected
        // at runtime. Its bookkeeping constructor does not initialize that guest
        // chain, so the child must use the same explicit-receiver factory path.
        var visited = new HashSet<Expr.ClassExpr>(ReferenceEqualityComparer.Instance);
        while (visited.Add(expression))
        {
            if (expression.SuperclassExpr == null
                || expression.SuperclassExpr is Expr.ClassExpr { Methods.Count: 0, Fields.Count: 0 }) return false;
            var parent = _classExprs.Builders[expression].BaseType;
            if (parent == null || parent == _types.Object) return true;
            if (parent.IsConstructedGenericType) parent = parent.GetGenericTypeDefinition();
            var parentExpression = _classExprs.Builders.FirstOrDefault(entry => ReferenceEquals(entry.Value, parent)).Key;
            if (parentExpression == null) return false;
            expression = parentExpression;
        }
        return false;
    }

    private static bool SupportsReceiverInitialization(Stmt.Class declaration)
    {
        // The finite runtime-parent path is ordinary fields and synchronous
        // methods. Other templates keep their existing CLR construction path.
        if (declaration.TypeParams is { Count: > 0 } || declaration.Accessors is { Count: > 0 }
            || declaration.AutoAccessors is { Count: > 0 }
            || declaration.Fields.Any(field => field.IsPrivate || field.ComputedKey != null)
            || declaration.Methods.Any(method => method.IsPrivate || method.IsAsync || method.IsGenerator
                || method.TypeParams is { Count: > 0 } || method.ComputedKey != null
                || method.Parameters.Any(parameter => parameter.IsParameterProperty || parameter.DestructuredProperties != null))) return false;
        return true;
    }

    private sealed class ConstructorReplacementVisitor : AstVisitorBase
    {
        public bool HasReplacement { get; private set; }
        protected override void VisitReturn(Stmt.Return statement) => HasReplacement |= statement.Value != null;
        protected override void VisitFunction(Stmt.Function statement) { }
        protected override void VisitArrowFunction(Expr.ArrowFunction expression) { }
        protected override void VisitClass(Stmt.Class statement) { }
        protected override void VisitClassExpr(Expr.ClassExpr expression) { }
    }

    private MethodBuilder DefineReceiverInitializer(TypeBuilder owner)
    {
        if (_receiverInitializers.TryGetValue(owner, out var existing)) return existing;
        var method = owner.DefineMethod("$InitializeReceiver", MethodAttributes.Public | MethodAttributes.Static,
            _types.Object, [_types.Object, _types.ObjectArray, _types.Object, _types.Object]);
        _receiverInitializers.Add(owner, method);
        return method;
    }

    private void EmitReceiverInitializer(TypeBuilder owner, Stmt.Class declaration, Expr.ClassExpr? expression = null)
    {
        // These entries deliberately use an explicit guest receiver. A runtime-selected
        // child need not be a CLR subtype of this template.
        var method = DefineReceiverInitializer(owner);
        var il = method.GetILGenerator();
        var ctx = expression == null ? CreateModuleMemberContext(il, method)
            : CreateClassExpressionContext(il, expression, owner, null, method);
        ctx.IsStrictMode = true;
        ctx.CurrentClassBuilder = owner;
        ctx.CurrentClassName = expression == null ? GetQualifiedClassDeclarationName(declaration) : _classExprs.Names[expression];
        ctx.EmittingTypeBuilder = owner;
        ctx.IsInstanceMethod = true;
        ctx.HasGuestReceiver = true;
        ctx.GuestThisVariableName = "__this";
        ctx.ClassDefinitionParameterIndex = 2;
        ctx.NewTargetParameterIndex = 3;
        ctx.DefineParameter("__this", 0, _types.Object);
        ApplyCapturedTopLevelVariableAccess(ctx);
        var emitter = new ILEmitter(ctx);
        var constructor = declaration.Methods.FirstOrDefault(source => source.Body != null && source.Name.Lexeme == "constructor" && !source.IsStatic);
        ctx.CurrentMethodReturnType = _types.Void;
        ctx.IsRuntimeReceiverInitializer = true;
        ConfigureClassConstructorReturns(ctx, constructor, declaration.SuperclassExpr != null, forceReceiver: true);
        if (declaration.SuperclassExpr == null)
            EmitConstructedClassPrototype(ctx);
        EmitOnceOnlyConstructorElements(ctx, () => EmitEffectiveClassElements(ctx, declaration.Fields, null, null,
            _ => throw new InvalidOperationException("Runtime receiver initializer cannot have computed fields.")));
        if (constructor?.Body != null && ReferencesArgumentsIdentifier(constructor.Body)
            && !constructor.Parameters.Any(parameter => parameter.Name.Lexeme == "arguments"))
        {
            var arguments = ctx.Locals.DeclareLocal("arguments", _types.ListOfObject);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, _runtime.Arguments.EnumerableCtor);
            il.Emit(OpCodes.Stloc, arguments);
        }
        foreach (var (parameter, index) in (constructor?.Parameters ?? []).Select((parameter, index) => (parameter, index)))
        {
            var local = ctx.Locals.DeclareLocal(parameter.Name.Lexeme, _types.Object);
            il.Emit(OpCodes.Ldarg_1);
            if (parameter.IsRest)
            {
                il.Emit(OpCodes.Newobj, _types.GetConstructor(_types.ListOfObject, _types.IEnumerableOfObject));
                il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldlen); il.Emit(OpCodes.Conv_I4); il.Emit(OpCodes.Ldc_I4, index);
                il.Emit(OpCodes.Call, _types.GetMethod(typeof(Math), "Min", _types.Int32, _types.Int32));
                il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldlen); il.Emit(OpCodes.Conv_I4);
                il.Emit(OpCodes.Ldc_I4, index); il.Emit(OpCodes.Sub); il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Call, _types.GetMethod(typeof(Math), "Max", _types.Int32, _types.Int32));
                il.Emit(OpCodes.Callvirt, _types.GetMethod(_types.ListOfObject, "GetRange", _types.Int32, _types.Int32));
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4, index);
                il.Emit(OpCodes.Call, _runtime.ClassDefinitions.ReadArgument);
            }
            il.Emit(OpCodes.Stloc, local);
        }
        foreach (var parameter in (constructor?.Parameters ?? []).Where(parameter => parameter.DefaultValue != null))
        {
            var local = ctx.Locals.GetLocal(parameter.Name.Lexeme)!;
            var present = il.DefineLabel();
            il.Emit(OpCodes.Ldloc, local); il.Emit(OpCodes.Isinst, _runtime.Sentinels.UndefinedType); il.Emit(OpCodes.Brfalse, present);
            emitter.EmitExpression(parameter.DefaultValue!); emitter.EmitBoxIfNeeded(parameter.DefaultValue!); il.Emit(OpCodes.Stloc, local);
            il.MarkLabel(present);
        }
        bool explicitSuper = constructor?.Body?.Any(ContainsSuperCall) == true;
        if (!explicitSuper && declaration.SuperclassExpr != null)
        {
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, _runtime.ClassDefinitions.GetParent);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Call, _runtime.ClassDefinitions.InitializeReceiver);
            il.Emit(OpCodes.Stloc, ctx.ClassConstructorReceiver!);
            SetReceiverCell();
        }
        else if (!explicitSuper)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stloc, ctx.ClassConstructorReceiver!);
            SetReceiverCell();
        }
        if (!explicitSuper) ctx.EmitConstructorOwnElements!();
        foreach (var statement in constructor?.Body ?? [])
            emitter.EmitStatement(statement);
        emitter.EmitClassConstructorFallthrough();
        il.Emit(OpCodes.Ret);
        emitter.FinalizeReturns();

        void SetReceiverCell()
        {
            il.Emit(OpCodes.Ldloc, ctx.ClassConstructorThisCell!);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldloc, ctx.ClassConstructorReceiver!);
            il.Emit(OpCodes.Stelem_Ref);
        }
    }

    private (TypeBuilder Type, ConstructorBuilder Constructor, MethodBuilder Method)? DefineDeclarationReceiverMethod(
        TypeBuilder owner, MethodBuilder original)
    {
        if (!_usesRuntimeParents) return null;
        var declaration = _classes.Declarations.FirstOrDefault(source => !_runtimeClassDeclarations.ContainsKey(source)
            && _classes.DeclarationNames.GetValueOrDefault(source) == owner.FullName);
        var source = declaration?.Methods.FirstOrDefault(source => source.Body != null && source.IsStatic == original.IsStatic && source.Name.Lexeme == original.Name);
        if (source == null || !SupportsReceiverInitialization(declaration!)) return null;
        var type = EmitTypeDefinitions.DefineType(_moduleBuilder, $"$ReceiverMember_{owner.FullName}_{original.Name}_{(source.IsStatic ? "Static" : "Instance")}", TypeAttributes.Public | TypeAttributes.Sealed, _types.Object);
        var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, _types.ObjectDefaultCtor); il.Emit(OpCodes.Ret);
        var method = type.DefineMethod("Invoke", MethodAttributes.Public, original.ReturnType,
            [_types.Object, .. source.Parameters.Select(parameter => parameter.IsRest ? _types.ListOfObject : _types.Object)]);
        MarkExpectsThis(method);
        if (!source.IsGenerator)
            MarkNonConstructible(method);
        MarkPadsUndefined(method);
        MarkFunctionLength(method, source.Parameters); MarkFunctionName(method, original.Name);
        EmitGuestMethodValueBody(method, source, () =>
        {
            var ctx = CreateModuleMemberContext(method.GetILGenerator(), method);
            ctx.CurrentClassBuilder = owner; ctx.CurrentClassName = owner.FullName;
            ctx.EmittingTypeBuilder = type; ctx.IsStrictMode = true; ctx.IsInstanceMethod = !source.IsStatic;
            ctx.IsStaticSuperContext = source.IsStatic;
            ctx.HasGuestReceiver = true; ctx.GuestThisVariableName = "__this";
            ApplyCapturedTopLevelVariableAccess(ctx);
            return ctx;
        }, owner.FullName!);
        type.CreateType();
        return (type, constructor, method);
    }
}
