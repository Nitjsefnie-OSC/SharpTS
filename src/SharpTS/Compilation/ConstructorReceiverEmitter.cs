using System.Reflection.Emit;

namespace SharpTS.Compilation;

internal static class ConstructorReceiverEmitter
{
    internal static void ReadCell(ILGenerator il, CompilationContext context)
    {
        il.Emit(OpCodes.Castclass, context.Types.ObjectArray);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldelem_Ref);
        var initialized = il.DefineLabel();
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Isinst, context.Runtime!.Sentinels.UndefinedType);
        il.Emit(OpCodes.Brfalse, initialized);
        il.Emit(OpCodes.Pop);
        GuestErrorEmitter.ThrowError(il, context.Runtime.Errors.CreateException,
            context.Runtime.Errors.ReferenceErrorConstructor,
            "Must call super constructor before accessing this");
        il.MarkLabel(initialized);
    }
}
