using System.Reflection;
using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

public partial class ILCompiler
{
    private readonly Dictionary<MethodBuilder, (TypeBuilder Type, ConstructorBuilder Constructor, MethodBuilder Method, FieldBuilder Value)>
        _declarationSymbolMethodValues = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MethodBuilder, (TypeBuilder Owner, Stmt.Function Source)>
        _declarationSymbolMethodValueOwners = new(ReferenceEqualityComparer.Instance);

    private (TypeBuilder Type, ConstructorBuilder Constructor, MethodBuilder Method, FieldBuilder Value)
        DefineDeclarationSymbolMethodValue(TypeBuilder owner, Stmt.Function source, MethodBuilder original)
    {
        if (_declarationSymbolMethodValues.TryGetValue(original, out var existing)) return existing;
        var type = EmitTypeDefinitions.DefineType(_moduleBuilder,
            $"$SymbolMember_{owner.FullName}_{original.Name}", TypeAttributes.Public | TypeAttributes.Sealed, _types.Object);
        var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, _types.ObjectDefaultCtor); il.Emit(OpCodes.Ret);
        // The helper's CLR target is bookkeeping; the guest receiver is explicit.
        // Erased slots keep one method value across CLR generic instantiations.
        var method = type.DefineMethod("Invoke", MethodAttributes.Public, original.ReturnType,
            [_types.Object, .. source.Parameters.Select(parameter => parameter.IsRest ? _types.ListOfObject : _types.Object)]);
        method.DefineParameter(1, ParameterAttributes.None, "__this");
        MarkExpectsThis(method); MarkPadsUndefined(method);
        MarkFunctionLength(method, source.Parameters); MarkFunctionName(method, original.Name);
        if (!source.IsGenerator) MarkNonConstructible(method);
        _declarationSymbolMethodValueOwners.Add(method, (owner, source));
        var collector = new GuestReceiverArrowCollector(_guestReceiverArrows);
        foreach (var statement in source.Body ?? []) collector.Visit(statement);
        EmitGuestMethodValueBody(method, source, () =>
        {
            var context = CreateModuleMemberContext(method.GetILGenerator(), method);
            ApplyDeclarationSymbolMethodValueContext(context, method);
            context.EmittingTypeBuilder = type;
            return context;
        }, owner.FullName!);
        var value = type.DefineField("Value", _runtime.FunctionValues.Type,
            FieldAttributes.Public | FieldAttributes.Static);
        type.CreateType();
        var result = (type, constructor, method, value);
        _declarationSymbolMethodValues.Add(original, result);
        return result;
    }

    private void ApplyDeclarationSymbolMethodValueContext(CompilationContext context, MethodBuilder method)
    {
        if (!_declarationSymbolMethodValueOwners.TryGetValue(method, out var owner)) return;
        context.CurrentClassBuilder = owner.Owner;
        context.CurrentClassName = owner.Owner.FullName;
        context.IsStrictMode = true;
        context.IsInstanceMethod = true;
        context.IsStaticSuperContext = false;
        context.HasGuestReceiver = true;
        context.UseNominalSuperCallBridge = true;
        context.GuestThisVariableName = "__this";
        foreach (var parameter in _classes.GenericParameters.Require(owner.Owner))
            context.GenericTypeParameters[parameter.Name] = _types.Object;
    }

    private bool CanOwnDeclaredSymbolMethodValue(TypeBuilder owner)
        => _classes.Declarations.Any(source => !_runtimeClassDeclarations.ContainsKey(source)
            && _classes.DeclarationNames.GetValueOrDefault(source) == owner.FullName);

    private void EmitDeclaredSymbolMethodDescriptor(ILGenerator il, TypeBuilder owner,
        Stmt.Function source, MethodBuilder original, LocalBuilder key, Type? descriptorOwner = null)
    {
        var adapter = DefineDeclarationSymbolMethodValue(owner, source, original);
        var haveValue = il.DefineLabel();
        il.Emit(OpCodes.Ldsfld, adapter.Value);
        il.Emit(OpCodes.Brtrue, haveValue);
        var target = il.DeclareLocal(_types.Object);
        il.Emit(OpCodes.Newobj, adapter.Constructor);
        il.Emit(OpCodes.Stloc, target);
        var metadata = il.DeclareLocal(_runtime.FunctionValues.Type);
        il.Emit(OpCodes.Ldloc, target);
        EmitMethodInfoLiteral(il, adapter.Method, adapter.Type);
        il.Emit(OpCodes.Newobj, _runtime.FunctionConstruction.Constructor);
        il.Emit(OpCodes.Stloc, metadata);
        il.Emit(OpCodes.Ldloc, target);
        EmitMethodInfoLiteral(il, adapter.Method, adapter.Type);
        il.Emit(OpCodes.Ldloc, key);
        il.Emit(OpCodes.Ldloc, metadata);
        il.Emit(OpCodes.Callvirt, _runtime.FunctionValues.NameGetter);
        il.Emit(OpCodes.Call, _runtime.Symbols.MethodName);
        il.Emit(OpCodes.Ldloc, metadata);
        il.Emit(OpCodes.Callvirt, _runtime.FunctionValues.LengthGetter);
        il.Emit(OpCodes.Newobj, _runtime.FunctionConstruction.CachedConstructor);
        il.Emit(OpCodes.Stsfld, adapter.Value);
        il.MarkLabel(haveValue);

        var storage = _runtime.DescriptorStorage;
        var descriptor = il.DeclareLocal(storage.DescriptorType);
        il.Emit(OpCodes.Newobj, storage.DescriptorConstructor); il.Emit(OpCodes.Stloc, descriptor);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Ldsfld, adapter.Value);
        il.Emit(OpCodes.Callvirt, storage.DescriptorValue.GetSetMethod()!);
        foreach (var property in new[] { storage.DescriptorWritable, storage.DescriptorConfigurable })
        {
            il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Callvirt, property.GetSetMethod()!);
        }
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Callvirt, storage.DescriptorEnumerable.GetSetMethod()!);
        Type self = descriptorOwner ?? (owner.IsGenericTypeDefinition
            ? EmitGenerics.MakeGenericType(owner, owner.GetGenericArguments()) : owner);
        il.Emit(OpCodes.Ldtoken, self); il.Emit(OpCodes.Call, _types.TypeGetTypeFromHandle);
        il.Emit(OpCodes.Call, _runtime.ClassPrototypes.Get);
        il.Emit(OpCodes.Call, _runtime.Symbols.GetStorage);
        il.Emit(OpCodes.Ldloc, key); il.Emit(OpCodes.Ldloc, descriptor);
        il.Emit(OpCodes.Callvirt, _types.GetMethod(_types.DictionaryObjectObject, "set_Item", _types.Object, _types.Object));
    }
}
