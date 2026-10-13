using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class ILCompiler
{
    private Type CanonicalGenericClassOwner(TypeBuilder owner)
    {
        if (!owner.IsGenericTypeDefinition) return owner;
        // Bound resolution may change as later classes are declared. Use the
        // arguments captured when this owner's actual CLR constraints were emitted.
        return EmitGenerics.MakeGenericType(owner,
            _classes.GenericParameters.RequireCanonicalArguments(owner).ToArray());
    }
}
