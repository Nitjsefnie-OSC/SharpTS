using System.Reflection.Emit;
using SharpTS.Compilation.Emitters;
using SharpTS.Parsing;

namespace SharpTS.Compilation.CallHandlers;

/// <summary>
/// Handles this.method() in static context (static blocks, static methods).
/// In static context, 'this' refers to the class constructor, so this.method() calls static methods.
/// </summary>
public class ThisStaticContextHandler : ICallHandler
{
    public int Priority => 76;

    public bool TryHandle(IEmitterContext emitter, Expr.Call call)
    {
        if (call.Optional || call.Callee is not Expr.Get thisGet || thisGet.Optional ||
            thisGet.Object is not Expr.This)
            return false;

        var ctx = emitter.Context;

        // Only applies in static context
        if (ctx.IsInstanceMethod || ctx.HasGuestReceiver || ctx.GuestThisVariableName != null
            || ctx.ClassDefinitionParameterIndex != null || ctx.ClassDefinitionVariableName != null || ctx.CurrentClassBuilder == null)
            return false;

        string? currentClassName = ctx.CurrentClassName;
        if (currentClassName == null)
            return false;

        if (!ctx.ClassRegistry!.TryGetCallableStaticMethod(currentClassName, thisGet.Name.Lexeme, ctx.CurrentClassBuilder, out _))
            return false;

        var il = emitter.IL;
        // Lexical metadata proves this is a static method reference, but its
        // actual constructor receiver selects the live method at runtime.
        var receiver = emitter.SpillBoxedArg(thisGet.Object);
        il.Emit(OpCodes.Ldloc, receiver);
        il.Emit(OpCodes.Ldstr, thisGet.Name.Lexeme);
        il.Emit(OpCodes.Call, ctx.Runtime!.ObjectRead.Property);
        var callable = emitter.SpillStackToObjectLocal();

        // Preserve lookup-before-arguments and retain both values across an
        // argument await/yield using the shared registered spill machinery.
        emitter.EmitArgsArrayWithSpread(call.Arguments);
        var arguments = il.DeclareLocal(ctx.Types.ObjectArray);
        il.Emit(OpCodes.Stloc, arguments);
        il.Emit(OpCodes.Ldloc, receiver);
        il.Emit(OpCodes.Ldloc, callable);
        il.Emit(OpCodes.Ldloc, arguments);
        il.Emit(OpCodes.Call, ctx.Runtime.Invocation.Method);
        emitter.SetStackUnknown();
        return true;
    }
}
