using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class ILEmitter
{
    protected override void EmitStoreVariable(string name)
    {
        if (_ctx.HasLoopBodyBindingAccess(name))
        {
            if (!_resolver.TryStoreVariable(name))
                throw new InvalidOperationException($"Missing lexical storage for '{name}'.");
            return;
        }
        base.EmitStoreVariable(name);
    }
}
