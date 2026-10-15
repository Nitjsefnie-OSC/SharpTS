using SharpTS.Parsing;
using SharpTS.TypeSystem.Exceptions;

namespace SharpTS.TypeSystem;

public partial class TypeChecker
{
    private Dictionary<Expr.NewTarget, NewTargetContextIndex.Context> _newTargetContexts =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, TypeInfo> _newTargetSourceTypes = new(ReferenceEqualityComparer.Instance);

    private void IndexNewTargetContexts(IEnumerable<Stmt> statements)
    {
        _newTargetContexts = NewTargetContextIndex.Collect(statements);
        _privateCompoundConstructors = PrivateCompoundConstructorIndex.Collect(statements);
        _newTargetSourceTypes.Clear();
    }

    internal TypeInfo VisitNewTarget(Expr.NewTarget expression)
    {
        if (!_newTargetContexts.TryGetValue(expression, out var context) && _currentModule is not null)
        {
            // A dynamic-import discovery pass can add a module after initial indexing.
            foreach (var entry in NewTargetContextIndex.Collect(_currentModule.Statements))
                _newTargetContexts[entry.Key] = entry.Value;
            _newTargetContexts.TryGetValue(expression, out context);
        }
        if (expression.SourceTypeScriptContextAllowed == false || !context.IsTypeScriptAllowed)
            throw new TypeCheckException("'new.target' is only allowed in the body of a function declaration, function expression, or constructor.",
                expression.Keyword.Line, tsCode: "TS17013");

        if (context.ClassOwner is { } classOwner)
        {
            TypeInfo? published = classOwner switch
            {
                Stmt.Class declaration => _typeMap.GetClassType(declaration),
                Expr.ClassExpr declaration => _typeMap.GetClassExprType(declaration),
                _ => null
            };
            // A default can be checked while members are still being collected.
            // Keep that mutable declaration live rather than freezing it early.
            return published ?? _newTargetSourceTypes.GetValueOrDefault(classOwner) ?? TypeInfo.Any.Shared;
        }
        if (context.Owner is { } owner && _newTargetSourceTypes.TryGetValue(owner, out var type))
            return type;

        // Defaults are checked before the complete callable signature is published.
        // Build its declared shape without checking defaults recursively or looking up
        // a mutable/shadowable source binding such as the function's name.
        var (parameters, returnAnnotation) = context.Owner switch
        {
            Stmt.Function function => (function.Parameters, function.ReturnType),
            Expr.ArrowFunction function => (function.Parameters, function.ReturnType),
            _ => (new List<Stmt.Parameter>(), (string?)null)
        };
        var parameterTypes = parameters.Select(parameter => parameter.Type is { } annotation
            ? ToTypeInfo(annotation) : TypeInfo.Any.Shared).ToList();
        int required = parameters.TakeWhile(parameter => !parameter.IsRest && !parameter.IsOptional && parameter.DefaultValue is null).Count();
        return new TypeInfo.Function(parameterTypes,
            returnAnnotation is null ? TypeInfo.Any.Shared : ToTypeInfo(returnAnnotation),
            required, parameters.Any(parameter => parameter.IsRest), null,
            parameters.Select(parameter => parameter.Name.Lexeme).ToList());
    }
}
