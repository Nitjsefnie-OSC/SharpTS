using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

public abstract partial class ExpressionEmitterBase
{
    private void EmitAssertedAssignment(Expr.AssertedAssignment expression)
    {
        Token? target = expression.LoweredAssignment switch
        {
            Expr.Assign assignment => assignment.Name,
            Expr.CompoundAssign assignment => assignment.Name,
            Expr.LogicalAssign assignment => assignment.Name,
            _ => null
        };
        if (target is null || Ctx.TypeMap?.IsConstBinding(target) != true)
        {
            // TargetSyntax is checker/editor data. The existing reference path
            // owns receiver/key evaluation, coercion, suspension and the store.
            EmitExpression(expression.LoweredAssignment);
            return;
        }

        switch (expression.LoweredAssignment)
        {
            case Expr.Assign assignment:
                EmitExpression(assignment.Value);
                IL.Emit(OpCodes.Pop);
                // Preserve simple assignment's RHS-before-TDZ ordering.
                EmitVariable(new Expr.Variable(target));
                IL.Emit(OpCodes.Pop);
                EmitConstAssignmentError();
                break;
            case Expr.CompoundAssign assignment:
            {
                EmitVariable(new Expr.Variable(target));
                EnsureBoxed();
                var current = _helpers.SpillStoreObject();
                var right = SpillBoxed(assignment.Value);
                IL.Emit(OpCodes.Ldloc, current);
                IL.Emit(OpCodes.Ldloc, right);
                EmitCompoundOperation(assignment.Operator.Type);
                IL.Emit(OpCodes.Pop);
                EmitConstAssignmentError();
                break;
            }
            case Expr.LogicalAssign assignment:
            {
                var end = IL.DefineLabel();
                EmitVariable(new Expr.Variable(target));
                EnsureBoxed();
                IL.Emit(OpCodes.Dup);
                EmitLogicalConditionCheck(assignment.Operator.Type, end);
                IL.Emit(OpCodes.Pop);
                EmitExpression(assignment.Value);
                IL.Emit(OpCodes.Pop);
                EmitConstAssignmentError();
                IL.MarkLabel(end);
                SetStackUnknown();
                break;
            }
        }
    }

    private void EmitConstAssignmentError()
    {
        GuestErrorEmitter.ThrowTypeError(IL, Ctx.Runtime!, "Assignment to constant variable.");
        // Expression callers still require a stack shape for the unreachable tail.
        EmitUndefinedConstant();
        SetStackUnknown();
    }
}
