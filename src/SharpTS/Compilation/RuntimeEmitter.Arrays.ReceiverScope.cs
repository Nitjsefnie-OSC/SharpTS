using System.Reflection;
using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    private void EmitArrayReceiverScopedAdapters(TypeBuilder typeBuilder, EmittedRuntime runtime)
    {
        var arrays = runtime.ArrayOperations;
        MethodBuilder[] callbacks = [
            arrays.Map, arrays.Filter, arrays.ForEach, arrays.Find, arrays.FindIndex,
            arrays.FindLast, arrays.FindLastIndex, arrays.Some, arrays.Every, arrays.FlatMap
        ];
        foreach (var helper in callbacks)
            EmitArrayReceiverScopedAdapter(typeBuilder, runtime, helper, stagesThisArg: true);

        MethodBuilder[] otherHelpers = [
            arrays.MapDirect, arrays.FilterDirect, arrays.FilterDirectBool, arrays.ForEachDirect,
            arrays.FindDirect, arrays.FindDirectBool, arrays.FindIndexDirect, arrays.FindIndexDirectBool,
            arrays.SomeDirect, arrays.SomeDirectBool, arrays.EveryDirect, arrays.EveryDirectBool,
            arrays.Reduce, arrays.ReduceDirect, arrays.ReduceRight, arrays.Flat, arrays.Includes,
            arrays.Sort, arrays.SortDirect, arrays.SortDirectNumber, arrays.SortNumeric,
            arrays.ToSorted, arrays.ToReversed, arrays.With, arrays.At, arrays.ToSpliced
        ];
        foreach (var helper in otherHelpers)
            EmitArrayReceiverScopedAdapter(typeBuilder, runtime, helper, stagesThisArg: false);
    }

    private void EmitArrayReceiverScopedAdapter(
        TypeBuilder typeBuilder, EmittedRuntime runtime, MethodBuilder helper, bool stagesThisArg)
    {
        var helperParameters = helper.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        Type[] parameters = stagesThisArg ? [.. helperParameters, _types.Object] : helperParameters;
        var method = typeBuilder.DefineMethod(helper.Name + "WithReceiverScope",
            MethodAttributes.Public | MethodAttributes.Static, helper.ReturnType, parameters);
        runtime.ArrayOperations.RegisterReceiverScope(helper, method);
        var il = method.GetILGenerator();
        var savedReceiver = il.DeclareLocal(_types.Object);
        var savedThisArg = il.DeclareLocal(_types.Object);
        var result = helper.ReturnType == _types.Void ? null : il.DeclareLocal(helper.ReturnType);

        il.Emit(OpCodes.Ldsfld, runtime.ArrayOperations.CurrentReceiverField);
        il.Emit(OpCodes.Stloc, savedReceiver);
        il.Emit(OpCodes.Ldsfld, runtime.ArrayOperations.CallbackThisArgField);
        il.Emit(OpCodes.Stloc, savedThisArg);

        // Typed nested calls must read their own list, even when an enclosing
        // prototype call has an observable receiver staged in these slots.
        // Keep the scope in a runtime method so argument expressions execute
        // before staging and exception handling never crosses a caller's stack.
        il.BeginExceptionBlock();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stsfld, runtime.ArrayOperations.CurrentReceiverField);
        if (stagesThisArg)
        {
            il.Emit(OpCodes.Ldarg, helperParameters.Length);
            il.Emit(OpCodes.Stsfld, runtime.ArrayOperations.CallbackThisArgField);
        }
        for (int index = 0; index < helperParameters.Length; index++)
            il.Emit(OpCodes.Ldarg, index);
        il.Emit(OpCodes.Call, helper);
        if (result != null)
            il.Emit(OpCodes.Stloc, result);
        il.BeginFinallyBlock();
        il.Emit(OpCodes.Ldloc, savedReceiver);
        il.Emit(OpCodes.Stsfld, runtime.ArrayOperations.CurrentReceiverField);
        il.Emit(OpCodes.Ldloc, savedThisArg);
        il.Emit(OpCodes.Stsfld, runtime.ArrayOperations.CallbackThisArgField);
        il.EndExceptionBlock();
        if (result != null)
            il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.Ret);
    }
}
