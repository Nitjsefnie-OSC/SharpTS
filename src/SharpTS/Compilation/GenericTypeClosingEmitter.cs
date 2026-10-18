using System.Reflection.Emit;

namespace SharpTS.Compilation;

/// <summary>Closes an actual CLR owner using supplied positions and its own erased bounds.</summary>
internal static class GenericTypeClosingEmitter
{
    internal static void Emit(ILGenerator il, TypeProvider types, LocalBuilder owner, LocalBuilder? supplied = null)
    {
        il.Emit(OpCodes.Ldloc, owner);
        il.Emit(OpCodes.Callvirt, types.GetMethodNoParams(types.Type, "GetGenericTypeDefinition"));
        il.Emit(OpCodes.Stloc, owner);
        var argumentsLocal = il.DeclareLocal(types.MakeArrayType(types.Type));
        il.Emit(OpCodes.Ldloc, owner);
        il.Emit(OpCodes.Callvirt, types.GetMethodNoParams(types.Type, "GetGenericArguments"));
        il.Emit(OpCodes.Stloc, argumentsLocal);
        var argumentIndex = il.DeclareLocal(types.Int32);
        var argumentType = il.DeclareLocal(types.Type);
        var constraints = il.DeclareLocal(types.MakeArrayType(types.Type));
        var remainingBounds = il.DeclareLocal(types.Int32);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, argumentIndex);
        var fillArguments = il.DefineLabel();
        var checkArguments = il.DefineLabel();
        il.Emit(OpCodes.Br, checkArguments);
        il.MarkLabel(fillArguments);
        il.Emit(OpCodes.Ldloc, argumentsLocal);
        il.Emit(OpCodes.Ldloc, argumentIndex);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Stloc, argumentType);
        il.Emit(OpCodes.Ldloc, argumentsLocal);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Stloc, remainingBounds);
        var followConstraint = il.DefineLabel();
        var useObject = il.DefineLabel();
        var storeArgument = il.DefineLabel();
        if (supplied != null)
        {
            var useBounds = il.DefineLabel();
            il.Emit(OpCodes.Ldloc, argumentIndex);
            il.Emit(OpCodes.Ldloc, supplied);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Conv_I4);
            il.Emit(OpCodes.Bge, useBounds);
            il.Emit(OpCodes.Ldloc, supplied);
            il.Emit(OpCodes.Ldloc, argumentIndex);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Stloc, argumentType);
            il.Emit(OpCodes.Br, storeArgument);
            il.MarkLabel(useBounds);
        }
        il.MarkLabel(followConstraint);
        il.Emit(OpCodes.Ldloc, argumentType);
        il.Emit(OpCodes.Callvirt, types.GetProperty(types.Type, "IsGenericParameter")!.GetGetMethod()!);
        il.Emit(OpCodes.Brfalse, storeArgument);
        // A valid parameter chain cannot visit more parameters than its owner.
        // Bound the walk without introducing a generic collection dependency.
        il.Emit(OpCodes.Ldloc, remainingBounds);
        il.Emit(OpCodes.Brfalse, useObject);
        il.Emit(OpCodes.Ldloc, remainingBounds);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Stloc, remainingBounds);
        il.Emit(OpCodes.Ldloc, argumentType);
        il.Emit(OpCodes.Callvirt, types.GetMethodNoParams(types.Type, "GetGenericParameterConstraints"));
        il.Emit(OpCodes.Stloc, constraints);
        il.Emit(OpCodes.Ldloc, constraints);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Brfalse, useObject);
        il.Emit(OpCodes.Ldloc, constraints);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Stloc, argumentType);
        il.Emit(OpCodes.Br, followConstraint);
        il.MarkLabel(useObject);
        il.Emit(OpCodes.Ldtoken, types.Object);
        il.Emit(OpCodes.Call, types.TypeGetTypeFromHandle);
        il.Emit(OpCodes.Stloc, argumentType);
        il.MarkLabel(storeArgument);
        il.Emit(OpCodes.Ldloc, argumentsLocal);
        il.Emit(OpCodes.Ldloc, argumentIndex);
        il.Emit(OpCodes.Ldloc, argumentType);
        il.Emit(OpCodes.Stelem_Ref);
        il.Emit(OpCodes.Ldloc, argumentIndex);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, argumentIndex);
        il.MarkLabel(checkArguments);
        il.Emit(OpCodes.Ldloc, argumentIndex);
        il.Emit(OpCodes.Ldloc, argumentsLocal);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Blt, fillArguments);
        il.Emit(OpCodes.Ldloc, owner);
        il.Emit(OpCodes.Ldloc, argumentsLocal);
        il.Emit(OpCodes.Callvirt, types.GetMethod(types.Type, "MakeGenericType", types.MakeArrayType(types.Type)));
        il.Emit(OpCodes.Stloc, owner);
    }
}
