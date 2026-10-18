using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

public abstract partial class ExpressionEmitterBase
{
    protected void EmitLogicalSetPrivate(Expr.LogicalSetPrivate logical)
    {
        var receiver = SpillBoxed(logical.Object);
        var reference = ResolvePrivateFieldReference(logical.Name, receiver);
        EmitPrivateFieldReferenceRead(reference);
        IL.Emit(OpCodes.Dup);

        var skip = IL.DefineLabel();
        var end = IL.DefineLabel();
        EmitLogicalConditionCheck(logical.Operator.Type, skip);

        // The current value remains on the skip path. Remove it on the write
        // path so an await/yield in the RHS suspends with an empty IL stack.
        IL.Emit(OpCodes.Pop);
        var value = SpillBoxed(logical.Value);
        EmitPrivateFieldReferenceWrite(reference, value);
        IL.Emit(OpCodes.Ldloc, value);
        IL.Emit(OpCodes.Br, end);

        IL.MarkLabel(skip);
        IL.MarkLabel(end);
        SetStackUnknown();
    }
}
