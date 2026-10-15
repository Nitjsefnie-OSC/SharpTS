using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    // Reflection returns boxed CLR primitives. Only actual numeric CLR values
    // are normalized here; ordinary guest values must retain their identity/type.
    private void EmitNormalizeClrNumericReturn(ILGenerator il) =>
        ClrNumericInteropEmitter.EmitNormalizeBoxed(il, _types);
}
