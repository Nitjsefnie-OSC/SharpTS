using System.Reflection;
using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    private void EmitArrayReceiverScopedAdapters(TypeBuilder typeBuilder, EmittedRuntime runtime)
    {
        var arrays = runtime.ArrayOperations;
        // RuntimeMethodBuilder cannot expose parameters before its owning type
        // has been created. Supply the declared signatures explicitly so both
        // executable and persisted assemblies use the same adapter emission.
        Scope([
            arrays.Map, arrays.Filter, arrays.ForEach, arrays.Find, arrays.FindIndex,
            arrays.FindLast, arrays.FindLastIndex, arrays.Some, arrays.Every, arrays.FlatMap
        ], [_types.ListOfObject, _types.Object], stagesThisArg: true);

        Scope([
            arrays.MapDirect, arrays.FilterDirect, arrays.ForEachDirect, arrays.FindDirect,
            arrays.FindIndexDirect, arrays.SomeDirect, arrays.EveryDirect
        ], [_types.ListOfObject, typeof(Func<object, object>)]);
        Scope([
            arrays.FilterDirectBool, arrays.FindDirectBool, arrays.FindIndexDirectBool,
            arrays.SomeDirectBool, arrays.EveryDirectBool
        ], [_types.ListOfObject, typeof(Func<object, bool>)]);
        Scope([arrays.Reduce, arrays.ReduceRight, arrays.With, arrays.ToSpliced],
            [_types.ListOfObject, _types.ObjectArray]);
        Scope([arrays.Flat, arrays.Sort, arrays.ToSorted, arrays.At],
            [_types.ListOfObject, _types.Object]);
        Scope([arrays.Includes], [_types.ListOfObject, _types.Object, _types.Object]);
        Scope([arrays.ToReversed], [_types.ListOfObject]);
        Scope([arrays.ReduceDirect],
            [_types.ListOfObject, typeof(Func<object, object, object>), _types.Object]);
        Scope([arrays.SortDirect], [_types.ListOfObject, typeof(Func<object, object, object>)]);
        Scope([arrays.SortDirectNumber], [_types.ListOfObject, typeof(Func<object, object, double>)]);
        Scope([arrays.SortNumeric],
            [_types.Object, typeof(Func<double, double, double>), typeof(Func<object, object, double>)]);

        void Scope(MethodBuilder[] helpers, Type[] parameters, bool stagesThisArg = false)
        {
            foreach (var helper in helpers)
                EmitArrayReceiverScopedAdapter(typeBuilder, runtime, helper, parameters, stagesThisArg);
        }
    }

    private void EmitArrayReceiverScopedAdapter(
        TypeBuilder typeBuilder, EmittedRuntime runtime, MethodBuilder helper,
        Type[] helperParameters, bool stagesThisArg)
    {
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
