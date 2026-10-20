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
        var normalizedLabel = il.DefineLabel();
        var completedLabel = il.DefineLabel();
        var queueContinuationLabel = il.DefineLabel();
        var nativeInput = il.DeclareLocal(_types.Boolean);
        var taskLocal = il.DeclareLocal(_types.TaskOfObject);

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
        il.Emit(OpCodes.Br, normalizedLabel);

        il.MarkLabel(nativePromiseLabel);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stloc, nativeInput);
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
        il.Emit(OpCodes.Br, normalizedLabel);

        il.MarkLabel(intrinsicPromiseLabel);
        // The intrinsic identity permits task reuse, including ignoring a
        // poisoned own then property. Coerce performs only the native unwrap.
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, promise.CoerceAwaitableToTaskMethod);

        il.MarkLabel(normalizedLabel);
        il.Emit(OpCodes.Stloc, taskLocal);
        il.Emit(OpCodes.Ldloc, taskLocal);
        il.Emit(OpCodes.Callvirt, _types.GetProperty(_types.Task, "IsCompleted").GetGetMethod()!);
        il.Emit(OpCodes.Brfalse, queueContinuationLabel);
        il.Emit(OpCodes.Ldloc, nativeInput);
        il.Emit(OpCodes.Brfalse, completedLabel);

        // A completed native Promise still resumes Await in a Promise job.
        // Ordinary completed coercions retain their existing policy; pending
        // tasks continue through the same single reaction facade as before.
        il.MarkLabel(queueContinuationLabel);
        // A synchronous-capable adoption bridge may settle inside then(resolve).
        // Give a pending await its own Promise job so guest code cannot resume
        // before the thenable finishes. The intrinsic no-handler path forwards
        // the raw value or exception without another guest then/constructor read.
        il.Emit(OpCodes.Ldloc, taskLocal);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Call, promise.Then);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(completedLabel);
        il.Emit(OpCodes.Ldloc, taskLocal);
        il.Emit(OpCodes.Ret);
    }
}
