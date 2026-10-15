using SharpTS.Parsing;
using SharpTS.TypeSystem;

namespace SharpTS.Compilation;

public partial class ILCompiler
{
    private bool ProvesNativeStoredFieldReturn(Stmt.Function function, Type[] parameterTypes)
    {
        if (_features?.UsesDynamicPropertyDescriptors != false ||
            _features.UsesObjectIntegrityMutation || _features.UsesClassPrototypeMutation ||
            function.Body is not [Stmt.Expression { Expr: Expr.Set
                { Object: Expr.Variable receiver, Value: Expr.Variable supplied } store },
                Stmt.Return { Value: not null } result])
            return false;

        int receiverIndex = function.Parameters.FindIndex(parameter => parameter.Name.Lexeme == receiver.Name.Lexeme);
        int suppliedIndex = function.Parameters.FindIndex(parameter => parameter.Name.Lexeme == supplied.Name.Lexeme);
        if (receiverIndex < 0 || suppliedIndex < 0 || suppliedIndex >= parameterTypes.Length ||
            receiverIndex >= parameterTypes.Length ||
            (parameterTypes[suppliedIndex] != _types.Double && parameterTypes[suppliedIndex] != _types.Boolean) ||
            function.Parameters[suppliedIndex] is { DefaultValue: not null } or { IsOptional: true } or { IsRest: true } ||
            _typeMap?.Get(receiver) is not TypeInfo.Instance instance ||
            _typeMapper.IsDynamicClassInstance(instance))
            return false;
        string? name = instance.ResolvedClassType switch
        {
            TypeInfo.Class owner => owner.Name,
            TypeInfo.MutableClass owner => owner.Name,
            _ => null
        };
        string? resolvedName = name is null ? null : GetDefinitionContext().ResolveClassName(instance, name);
        if (resolvedName is null || !_classes.Builders.TryGetValue(resolvedName, out var receiverBuilder) ||
            !GetClassRegistry().TryGetPrimitiveGuestField(resolvedName, store.Name.Lexeme, out var field) ||
            field.NativeType != parameterTypes[suppliedIndex] || !GetClassRegistry().CanUseNativePrimitiveField(field) ||
            !EmitterTypeHelpers.TryResolveInstanceDispatch(receiverBuilder, field.NativeSetter, _programType, out _, out _) ||
            !EmitterTypeHelpers.TryResolveInstanceDispatch(receiverBuilder, field.NativeGetter, _programType, out _, out _) ||
            !HasClosedPrimitiveFieldReturnHierarchy(instance))
            return false;

        bool StoredRead(Expr value) => value is Expr.Get { Object: Expr.Variable target } get &&
            target.Name.Lexeme == receiver.Name.Lexeme && get.Name.Lexeme == store.Name.Lexeme;
        return StoredRead(result.Value) || field.NativeType == _types.Double &&
            result.Value is Expr.Binary { Operator.Type: TokenType.PLUS } plus &&
            ((StoredRead(plus.Left) && plus.Right is Expr.Literal { Value: double }) ||
             (plus.Left is Expr.Literal { Value: double } && StoredRead(plus.Right)));
    }

    private bool HasClosedPrimitiveFieldReturnHierarchy(TypeInfo.Instance instance)
    {
        int ownerId = instance.ResolvedClassType switch
        {
            TypeInfo.Class owner => owner.Core.DeclarationId,
            TypeInfo.MutableClass owner => owner.DeclarationId,
            _ => 0
        };
        if (ownerId == 0 || _typeMap is null) return false;
        // Signatures are declared in source order, before a later derived type's
        // helpers necessarily exist. Consult the complete checked hierarchy too.
        foreach (var candidate in _typeMap.CheckedClassDefinitions)
        {
            for (TypeInfo? parent = candidate.Superclass; parent is not null;)
            {
                var core = ClassInfoAccessor.Get(parent, type => type.Core, type => type.Core);
                if (core is null) return false;
                if (core.DeclarationId == ownerId) return false;
                parent = core.Superclass;
            }
        }
        // Evaluated heritage may denote a subclass without a nominal checked
        // superclass identity. Decline this small optimization in that case.
        return !_typeMap.HasClassExpressionHeritage;
    }
}
