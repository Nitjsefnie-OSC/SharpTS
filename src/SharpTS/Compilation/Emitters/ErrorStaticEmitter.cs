using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation.Emitters;

/// <summary>Emits the ES2026 <c>Error.isError</c> static method.</summary>
public sealed class ErrorStaticEmitter : IStaticTypeEmitterStrategy
{
    public bool TryEmitStaticCall(IEmitterContext emitter, string methodName, List<Expr> arguments)
        => false;

    public bool TryEmitStaticPropertyGet(IEmitterContext emitter, string propertyName)
    {
        if (propertyName != "isError") return false;
        var ctx = emitter.Context;
        // Runtime lookup observes descriptor overrides and deletion. Calls use
        // ordinary receiver-aware invocation so the current property is called.
        ctx.IL.Emit(OpCodes.Ldtoken, ctx.Runtime!.Errors.Type);
        ctx.IL.Emit(OpCodes.Call, ctx.Types.TypeGetTypeFromHandle);
        ctx.IL.Emit(OpCodes.Ldstr, propertyName);
        ctx.IL.Emit(OpCodes.Call, ctx.Runtime.ObjectRead.Property);
        return true;
    }

    public bool HasStaticProperty(string memberName) => memberName == "isError";
}
