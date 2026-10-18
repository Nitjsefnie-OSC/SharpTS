using SharpTS.Parsing;
using SharpTS.Runtime;
using SharpTS.Runtime.Exceptions;
using SharpTS.Runtime.Types;

namespace SharpTS.Execution;

public partial class Interpreter
{
    private async ValueTask<RuntimeValue> EvaluateAssertedAssignmentCore(
        IEvaluationContext context, Expr.AssertedAssignment expression)
    {
        Token? target = expression.LoweredAssignment switch
        {
            Expr.Assign assignment => assignment.Name,
            Expr.CompoundAssign assignment => assignment.Name,
            Expr.LogicalAssign assignment => assignment.Name,
            _ => null
        };
        if (target is null || _typeMap?.IsConstBinding(target) != true)
            return await context.EvaluateExprAsync(expression.LoweredAssignment);

        // TypeScript assertions erase the checker's const-reference restriction,
        // but do not erase the immutable JavaScript binding at PutValue.
        switch (expression.LoweredAssignment)
        {
            case Expr.Assign assignment:
                await context.EvaluateExprAsync(assignment.Value);
                // Get the identifier only after the RHS so a TDZ failure keeps
                // simple assignment's evaluation order.
                LookupVariableRV(target, assignment);
                break;
            case Expr.CompoundAssign assignment:
            {
                RuntimeValue current = LookupVariableRV(target, assignment);
                RuntimeValue right = await context.EvaluateExprAsync(assignment.Value);
                ApplyCompoundOperatorRV(assignment.Operator.Type, current, right);
                break;
            }
            case Expr.LogicalAssign assignment:
            {
                RuntimeValue current = LookupVariableRV(target, assignment);
                bool writes = assignment.Operator.Type switch
                {
                    TokenType.AND_AND_EQUAL => current.IsTruthy(),
                    TokenType.OR_OR_EQUAL => !current.IsTruthy(),
                    TokenType.QUESTION_QUESTION_EQUAL => current.IsNullish,
                    _ => true
                };
                if (!writes) return current;
                await context.EvaluateExprAsync(assignment.Value);
                break;
            }
        }
        throw new ThrowException(new SharpTSTypeError("Assignment to constant variable."));
    }
}
