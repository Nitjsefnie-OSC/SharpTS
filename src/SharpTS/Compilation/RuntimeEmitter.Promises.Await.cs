using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    /// <summary>
    /// Implements Await's intrinsic PromiseResolve identity decision without
    /// changing the resolving-function policy of shared coercion consumers.
    /// </summary>
    private void EmitAwaitValueToTask(EmittedRuntime runtime)
    {
        var promise = runtime.RequirePromise();
        var il = promise.AwaitValueToTaskMethod.GetILGenerator();
        var nativePromiseLabel = il.DefineLabel();
        var intrinsicPromiseLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Isinst, promise.Type);
        il.Emit(OpCodes.Brtrue, nativePromiseLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Isinst, _types.TaskOfObject);
        il.Emit(OpCodes.Brtrue, nativePromiseLabel);

        // Ordinary values retain the existing coercion policy. Only native
        // promises need the observable constructor test introduced here.
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, promise.CoerceAwaitableToTaskMethod);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(nativePromiseLabel);
        // Get constructor once, before choosing a fast path. A subclass may
        // override it with intrinsic Promise, and a base promise may replace it.
        // An abrupt constructor getter propagates from PromiseResolve itself.
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldstr, "constructor");
        il.Emit(OpCodes.Call, runtime.ObjectRead.Property);
        il.Emit(OpCodes.Ldtoken, _types.TaskOfObject);
        il.Emit(OpCodes.Call, _types.GetMethod(
            _types.Type, "GetTypeFromHandle", _types.RuntimeTypeHandle));
        il.Emit(OpCodes.Beq, intrinsicPromiseLabel);

        // ResolveValue captures the live then once and queues invocation with
        // the original receiver. Coerce would incorrectly unwrap this promise.
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, promise.ResolveValueMethod);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(intrinsicPromiseLabel);
        // The intrinsic identity permits task reuse, including ignoring a
        // poisoned own then property. Coerce performs only the native unwrap.
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, promise.CoerceAwaitableToTaskMethod);
        il.Emit(OpCodes.Ret);
    }
}
