using System.Reflection;
using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    private readonly record struct ClassConstructorReturnInputs(EmittedErrorRuntime Errors,
        Type UndefinedType, Type SymbolType);

    private void EmitClassConstructorReturns(TypeBuilder owner, EmittedDynamicConstructionRuntime construction,
        ClassConstructorReturnInputs inputs)
    {
        var tableType = _types.ConditionalWeakTableObjectObject;
        var table = owner.DefineField("_classConstructorReturns", tableType, FieldAttributes.Private | FieldAttributes.Static);
        construction.RecordClassReturn = owner.DefineMethod("RecordClassConstructorReturn",
            MethodAttributes.Public | MethodAttributes.Static, _types.Void,
            [_types.Object, _types.Object, _types.Boolean, _types.Object]);
        construction.ResolveClassReturn = owner.DefineMethod("ResolveClassConstructorReturn",
            MethodAttributes.Public | MethodAttributes.Static, _types.Object, [_types.Object]);

        var il = construction.RecordClassReturn.GetILGenerator();
        var ready = il.DefineLabel();
        il.Emit(OpCodes.Ldsfld, table);
        il.Emit(OpCodes.Brtrue, ready);
        il.Emit(OpCodes.Ldsflda, table);
        il.Emit(OpCodes.Newobj, _types.GetDefaultConstructor(tableType));
        il.Emit(OpCodes.Ldnull);
        var compareExchange = typeof(Interlocked).GetMethods().Single(method =>
            method.Name == "CompareExchange" && method.IsGenericMethodDefinition && method.GetParameters().Length == 3);
        il.Emit(OpCodes.Call, EmitGenerics.MakeGenericMethod(compareExchange, tableType));
        il.Emit(OpCodes.Pop);
        il.MarkLabel(ready);
        il.Emit(OpCodes.Ldsfld, table);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_3);
        il.Emit(OpCodes.Newarr, _types.Object);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stelem_Ref);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Box, _types.Boolean);
        il.Emit(OpCodes.Stelem_Ref);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Stelem_Ref);
        il.Emit(OpCodes.Callvirt, _types.GetMethod(tableType, "AddOrUpdate", _types.Object, _types.Object));
        il.Emit(OpCodes.Ret);

        // Preserve the raw completion until the constructor has completed all finally
        // clauses. An overriding finally return must replace even an invalid derived
        // primitive return before the construction caller applies [[Construct]].
        il = construction.ResolveClassReturn.GetILGenerator();
        var original = il.DefineLabel();
        var primitive = il.DefineLabel();
        var fallback = il.DefineLabel();
        var completion = il.DeclareLocal(_types.Object);
        var values = il.DeclareLocal(_types.ObjectArray);
        var value = il.DeclareLocal(_types.Object);
        il.Emit(OpCodes.Ldsfld, table);
        il.Emit(OpCodes.Brfalse, original);
        il.Emit(OpCodes.Ldsfld, table);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloca, completion);
        il.Emit(OpCodes.Callvirt, _types.GetMethod(tableType, "TryGetValue", _types.Object, _types.Object.MakeByRefType()));
        il.Emit(OpCodes.Brfalse, original);
        il.Emit(OpCodes.Ldloc, completion);
        il.Emit(OpCodes.Castclass, _types.ObjectArray);
        il.Emit(OpCodes.Stloc, values);
        il.Emit(OpCodes.Ldloc, values);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Stloc, value);
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Brfalse, primitive);
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Isinst, inputs.UndefinedType);
        il.Emit(OpCodes.Brtrue, fallback);
        foreach (var type in new[] { _types.Double, _types.Boolean, _types.String, _types.BigInteger, inputs.SymbolType })
        {
            il.Emit(OpCodes.Ldloc, value);
            il.Emit(OpCodes.Isinst, type);
            il.Emit(OpCodes.Brtrue, primitive);
        }
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(primitive);
        il.Emit(OpCodes.Ldloc, values);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Unbox_Any, _types.Boolean);
        il.Emit(OpCodes.Brfalse, fallback);
        GuestErrorEmitter.ThrowError(il, inputs.Errors.CreateException, inputs.Errors.TypeErrorConstructor,
            "Derived constructors may only return object or undefined");
        il.MarkLabel(fallback);
        il.Emit(OpCodes.Ldloc, values);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(original);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ret);
    }
}
