using System.Reflection.Emit;
using SharpTS.Compilation.Emitters;
using SharpTS.Parsing;

namespace SharpTS.Compilation.CallHandlers;

/// <summary>
/// Handles static method calls on imported classes (import X = require('./module') where module exports a class).
/// </summary>
public class ImportedClassStaticHandler : ICallHandler
{
    public int Priority => 72;

    public bool TryHandle(IEmitterContext emitter, Expr.Call call)
    {
        if (call.Callee is not Expr.Get importedGet ||
            importedGet.Object is not Expr.Variable importedVar)
            return false;

        var ctx = emitter.Context;
        if (ctx.ImportedClassAliases?.TryGetValue(importedVar.Name.Lexeme, out var importedQualifiedClassName) != true ||
            importedQualifiedClassName == null ||
            !ctx.Classes.TryGetValue(importedQualifiedClassName, out var importedClassBuilder))
            return false;

        if (!ctx.ClassRegistry!.TryGetCallableStaticMethod(importedQualifiedClassName, importedGet.Name.Lexeme, importedClassBuilder, out _))
            return false;

        var il = emitter.IL;
        // Keep the imported constructor value, rather than the method's declaring
        // owner: an inherited or erased generic method can have a different owner.
        // Runtime invocation supplies this receiver and restores it even on throw.
        var receiver = emitter.SpillBoxedArg(importedGet.Object);
        il.Emit(OpCodes.Ldloc, receiver);
        il.Emit(OpCodes.Ldstr, importedGet.Name.Lexeme);
        il.Emit(OpCodes.Call, ctx.Runtime!.ObjectRead.Property);
        var callable = emitter.SpillStackToObjectLocal();

        // Resolve the callable before arguments, and retain both in registered
        // spill locals so a pending argument can suspend with an empty stack.
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
