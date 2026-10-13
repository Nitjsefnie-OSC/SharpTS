using System.Reflection;
using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

public abstract partial class ExpressionEmitterBase
{
    /// <summary>Publishes the caller list before deriving fixed/default/rest CLR slots from it.</summary>
    private void EmitArgumentsObservingCall(List<Expr> expressions, ParameterInfo[] parameters, bool hasRest)
    {
        EmitArgsArrayWithSpread(expressions);
        var arguments = IL.DeclareLocal(Types.ObjectArray);
        IL.Emit(OpCodes.Stloc, arguments);
        IL.Emit(OpCodes.Ldloc, arguments);
        IL.Emit(OpCodes.Stsfld, Ctx.Runtime!.Arguments.CurrentField);

        int fixedCount = parameters.Length - (hasRest ? 1 : 0);
        for (int index = 0; index < fixedCount; index++)
        {
            var missing = IL.DefineLabel();
            var ready = IL.DefineLabel();
            IL.Emit(OpCodes.Ldloc, arguments);
            IL.Emit(OpCodes.Ldlen);
            IL.Emit(OpCodes.Conv_I4);
            IL.Emit(OpCodes.Ldc_I4, index);
            IL.Emit(OpCodes.Ble, missing);
            IL.Emit(OpCodes.Ldloc, arguments);
            IL.Emit(OpCodes.Ldc_I4, index);
            IL.Emit(OpCodes.Ldelem_Ref);
            EmitCoerceBoxedToType(parameters[index].ParameterType);
            IL.Emit(OpCodes.Br, ready);
            IL.MarkLabel(missing);
            EmitOmittedArgument(parameters[index].ParameterType);
            IL.MarkLabel(ready);
        }
        if (!hasRest) return;

        // The raw array remains intact; collecting the rest parameter must not
        // alter the length/positions observed by the eager arguments snapshot.
        var rest = IL.DeclareLocal(Ctx.Runtime!.ArrayStorage.Type);
        IL.Emit(OpCodes.Ldc_I4_0);
        IL.Emit(OpCodes.Newobj, Ctx.Runtime.ArrayStorage.RestCtor);
        IL.Emit(OpCodes.Stloc, rest);
        var next = IL.DeclareLocal(Types.Int32);
        IL.Emit(OpCodes.Ldc_I4, fixedCount);
        IL.Emit(OpCodes.Stloc, next);
        var loop = IL.DefineLabel();
        var done = IL.DefineLabel();
        IL.MarkLabel(loop);
        IL.Emit(OpCodes.Ldloc, next);
        IL.Emit(OpCodes.Ldloc, arguments);
        IL.Emit(OpCodes.Ldlen);
        IL.Emit(OpCodes.Conv_I4);
        IL.Emit(OpCodes.Bge, done);
        IL.Emit(OpCodes.Ldloc, rest);
        IL.Emit(OpCodes.Ldloc, arguments);
        IL.Emit(OpCodes.Ldloc, next);
        IL.Emit(OpCodes.Ldelem_Ref);
        IL.Emit(OpCodes.Call, Ctx.Runtime.ArrayStorage.AppendRest);
        IL.Emit(OpCodes.Ldloc, next);
        IL.Emit(OpCodes.Ldc_I4_1);
        IL.Emit(OpCodes.Add);
        IL.Emit(OpCodes.Stloc, next);
        IL.Emit(OpCodes.Br, loop);
        IL.MarkLabel(done);
        IL.Emit(OpCodes.Ldloc, rest);
    }
}
