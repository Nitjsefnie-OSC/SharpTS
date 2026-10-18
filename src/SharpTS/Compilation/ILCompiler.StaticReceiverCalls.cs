using System.Reflection;
using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class ILCompiler
{
    private readonly Dictionary<(TypeBuilder Receiver, MethodInfo Target), MethodBuilder> _staticReceiverCalls = [];

    private MethodInfo GetOrCreateStaticReceiverCall(TypeBuilder receiver, MethodInfo target)
    {
        if (_staticReceiverCalls.TryGetValue((receiver, target), out var existing))
            return existing;

        var parameters = target.GetParameters();
        var wrapper = _programType.DefineMethod($"$staticReceiverCall${_staticReceiverCalls.Count}",
            MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig,
            target.ReturnType, parameters.Select(parameter => parameter.ParameterType).ToArray());
        MarkCompilerGenerated(wrapper);
        _staticReceiverCalls.Add((receiver, target), wrapper);

        var il = wrapper.GetILGenerator();
        var previousThis = il.DeclareLocal(_types.Object);
        LocalBuilder? result = target.ReturnType == _types.Void ? null : il.DeclareLocal(target.ReturnType);
        var done = il.DefineLabel();
        il.Emit(OpCodes.Ldsfld, _runtime!.FunctionValues.CurrentThisField);
        il.Emit(OpCodes.Stloc, previousThis);

        il.BeginExceptionBlock();
        // The requested constructor can differ from the closed generic declaring owner.
        il.Emit(OpCodes.Ldtoken, receiver);
        il.Emit(OpCodes.Call, _types.TypeGetTypeFromHandle);
        il.Emit(OpCodes.Stsfld, _runtime.FunctionValues.CurrentThisField);
        for (int index = 0; index < parameters.Length; index++)
            il.Emit(OpCodes.Ldarg, index);
        il.Emit(OpCodes.Call, target);
        if (result is not null)
            il.Emit(OpCodes.Stloc, result);
        il.Emit(OpCodes.Leave, done);

        il.BeginFinallyBlock();
        il.Emit(OpCodes.Ldloc, previousThis);
        il.Emit(OpCodes.Stsfld, _runtime.FunctionValues.CurrentThisField);
        il.EndExceptionBlock();

        il.MarkLabel(done);
        if (result is not null)
            il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.Ret);
        return wrapper;
    }
}
