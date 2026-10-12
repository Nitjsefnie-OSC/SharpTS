using System.Reflection;
using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

public partial class ILCompiler
{
    private readonly Dictionary<Stmt.Accessor, (TypeBuilder Type, ConstructorBuilder Constructor, MethodBuilder Method, FieldBuilder Value)>
        _declarationAccessorValues = new(ReferenceEqualityComparer.Instance);

    private (TypeBuilder Type, ConstructorBuilder Constructor, MethodBuilder Method, FieldBuilder Value) DefineDeclarationAccessorValue(
        TypeBuilder owner, Stmt.Accessor accessor, MethodBuilder original)
    {
        if (_declarationAccessorValues.TryGetValue(accessor, out var existing)) return existing;
        var type = EmitTypeDefinitions.DefineType(_moduleBuilder,
            $"$AccessorMember_{owner.FullName}_{original.Name}_{_declarationAccessorValues.Count}", TypeAttributes.Public | TypeAttributes.Sealed, _types.Object);
        var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, _types.ObjectDefaultCtor); il.Emit(OpCodes.Ret);
        var parameters = accessor.SetterParam == null ? new List<Stmt.Parameter>() : [accessor.SetterParam];
        // These callable slots are erased independently of the CLR owner's generic slots.
        var method = type.DefineMethod("Invoke", MethodAttributes.Public, _types.Object,
            [_types.Object, .. parameters.Select(_ => _types.Object)]);
        method.DefineParameter(1, ParameterAttributes.None, "__this");
        MarkExpectsThis(method); MarkNonConstructible(method); MarkPadsUndefined(method);
        MarkFunctionLength(method, parameters);
        MarkFunctionName(method, accessor.ComputedKey == null
            ? $"{(accessor.Kind.Type == TokenType.GET ? "get" : "set")} {accessor.Name.Lexeme}" : original.Name);
        var source = new Stmt.Function(accessor.Name, null, null, parameters, accessor.Body, null, IsStatic: accessor.IsStatic);
        EmitGuestMethodValueBody(method, source, () =>
        {
            var ctx = CreateModuleMemberContext(method.GetILGenerator(), method);
            ctx.IsStrictMode = true; ctx.CurrentClassName = owner.FullName; ctx.CurrentClassBuilder = owner;
            ctx.EmittingTypeBuilder = type; ctx.IsInstanceMethod = !accessor.IsStatic;
            ctx.IsStaticSuperContext = accessor.IsStatic; ctx.HasGuestReceiver = true; ctx.GuestThisVariableName = "__this";
            var genericParameters = _classes.GenericParameters.Require(owner);
            var canonicalArguments = owner.IsGenericTypeDefinition
                ? CanonicalGenericClassOwner(owner).GetGenericArguments() : Type.EmptyTypes;
            for (int i = 0; i < genericParameters.Count; i++)
                ctx.GenericTypeParameters[genericParameters[i].Name] = canonicalArguments[i];
            ApplyCapturedTopLevelVariableAccess(ctx, memberBodyExports: true);
            return ctx;
        }, owner.FullName!);
        // A generic owner's initializer runs for every closed CLR instantiation.
        // Keep the JavaScript accessor value independent of those initializations.
        var value = type.DefineField("Value", _types.Object,
            FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly);
        var initializer = type.DefineTypeInitializer().GetILGenerator();
        initializer.Emit(OpCodes.Newobj, constructor);
        EmitMethodInfoLiteral(initializer, method, type);
        initializer.Emit(OpCodes.Newobj, _runtime.FunctionConstruction.Constructor);
        initializer.Emit(OpCodes.Stsfld, value);
        initializer.Emit(OpCodes.Ret);
        type.CreateType();
        var result = (type, constructor, method, value);
        _declarationAccessorValues.Add(accessor, result);
        return result;
    }

    private void EmitDeclaredAccessorDescriptor(ILGenerator il, TypeBuilder owner, Stmt.Accessor accessor,
        MethodBuilder method, Action emitKey, Type? descriptorOwner = null)
    {
        // Evaluated class expressions install descriptors on their owned definition/prototype.
        if (_classExprs.Builders.Values.Any(builder => ReferenceEquals(builder, owner)) || accessor.IsAbstract) return;
        var registrationDone = il.DefineLabel();
        if (accessor.IsStatic && owner.IsGenericTypeDefinition && descriptorOwner == null)
        {
            // Constructor reflection uses the open owner token. Only its canonical
            // erased initializer installs those slots; typed instance initialization
            // must not resurrect deleted or redefined JavaScript properties.
            il.Emit(OpCodes.Ldtoken, EmitGenerics.MakeGenericType(owner, owner.GetGenericArguments()));
            il.Emit(OpCodes.Call, _types.TypeGetTypeFromHandle);
            il.Emit(OpCodes.Ldtoken, CanonicalGenericClassOwner(owner));
            il.Emit(OpCodes.Call, _types.TypeGetTypeFromHandle);
            il.Emit(OpCodes.Bne_Un, registrationDone);
        }
        var storage = _runtime.DescriptorStorage;
        var receiver = il.DeclareLocal(_types.Object);
        Type self = accessor.IsStatic ? owner : descriptorOwner ?? (owner.IsGenericTypeDefinition
            ? EmitGenerics.MakeGenericType(owner, owner.GetGenericArguments()) : owner);
        il.Emit(OpCodes.Ldtoken, self); il.Emit(OpCodes.Call, _types.TypeGetTypeFromHandle);
        if (!accessor.IsStatic) il.Emit(OpCodes.Call, _runtime.ClassPrototypes.Get);
        il.Emit(OpCodes.Stloc, receiver);
        var key = il.DeclareLocal(_types.Object);
        emitKey(); il.Emit(OpCodes.Stloc, key);
        var descriptor = il.DeclareLocal(storage.DescriptorType);
        var symbol = il.DefineLabel(); var haveDescriptor = il.DefineLabel(); var useExisting = il.DefineLabel(); var fresh = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, key); il.Emit(OpCodes.Isinst, _types.String); il.Emit(OpCodes.Brfalse, symbol);
        il.Emit(OpCodes.Ldloc, receiver); il.Emit(OpCodes.Ldloc, key); il.Emit(OpCodes.Castclass, _types.String);
        il.Emit(OpCodes.Call, storage.GetPropertyDescriptor); il.Emit(OpCodes.Stloc, descriptor);
        il.Emit(OpCodes.Br, haveDescriptor);
        il.MarkLabel(symbol);
        var existing = il.DeclareLocal(_types.Object);
        il.Emit(OpCodes.Ldloc, receiver); il.Emit(OpCodes.Call, _runtime.Symbols.GetStorage);
        il.Emit(OpCodes.Ldloc, key); il.Emit(OpCodes.Ldloca, existing);
        il.Emit(OpCodes.Callvirt, _types.GetMethod(_types.DictionaryObjectObject, "TryGetValue")); il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldloc, existing); il.Emit(OpCodes.Isinst, storage.DescriptorType); il.Emit(OpCodes.Stloc, descriptor);
        il.MarkLabel(haveDescriptor);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Brfalse, fresh);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Callvirt, storage.DescriptorGetter.GetGetMethod()!);
        il.Emit(OpCodes.Brtrue, useExisting);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Callvirt, storage.DescriptorSetter.GetGetMethod()!);
        il.Emit(OpCodes.Brtrue, useExisting);
        il.MarkLabel(fresh);
        il.Emit(OpCodes.Newobj, storage.DescriptorConstructor); il.Emit(OpCodes.Stloc, descriptor);
        il.MarkLabel(useExisting);
        var adapter = DefineDeclarationAccessorValue(owner, accessor, method);
        il.Emit(OpCodes.Ldloc, descriptor);
        il.Emit(OpCodes.Ldsfld, adapter.Value);
        il.Emit(OpCodes.Callvirt, (accessor.Kind.Type == TokenType.GET ? storage.DescriptorGetter : storage.DescriptorSetter).GetSetMethod()!);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Callvirt, storage.DescriptorEnumerable.GetSetMethod()!);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Callvirt, storage.DescriptorConfigurable.GetSetMethod()!);
        var storeSymbol = il.DefineLabel(); var done = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, key); il.Emit(OpCodes.Isinst, _types.String); il.Emit(OpCodes.Brfalse, storeSymbol);
        il.Emit(OpCodes.Ldloc, receiver); il.Emit(OpCodes.Ldloc, key); il.Emit(OpCodes.Castclass, _types.String);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Call, storage.DefineProperty); il.Emit(OpCodes.Pop); il.Emit(OpCodes.Br, done);
        il.MarkLabel(storeSymbol);
        il.Emit(OpCodes.Ldloc, receiver); il.Emit(OpCodes.Call, _runtime.Symbols.GetStorage);
        il.Emit(OpCodes.Ldloc, key); il.Emit(OpCodes.Ldloc, descriptor);
        il.Emit(OpCodes.Callvirt, _types.GetMethod(_types.DictionaryObjectObject, "set_Item", _types.Object, _types.Object));
        il.MarkLabel(done);
        il.MarkLabel(registrationDone);
    }
}
