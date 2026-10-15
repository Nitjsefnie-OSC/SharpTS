using SharpTS.Parsing;

namespace SharpTS.TypeSystem;

public partial class TypeChecker
{
    // Constructor values have a static member surface. Do not let the general
    // structural lookup mistake an instance @@hasInstance for that surface.
    private TypeInfo? GetFunctionHasInstanceReadType(TypeInfo receiver, Expr index)
    {
        const string name = "@@hasInstance";
        if (receiver is TypeInfo.InstantiatedGeneric
            { GenericDefinition: TypeInfo.GenericInterface } instantiatedInterface &&
            FlattenInstantiatedInterface(instantiatedInterface) is { } flattenedInterface)
            receiver = flattenedInterface;

        if (receiver is TypeInfo.Class or TypeInfo.GenericClass or
            TypeInfo.InstantiatedGeneric { GenericDefinition: TypeInfo.GenericClass })
        {
            var memberName = new Token(TokenType.IDENTIFIER, name, null,
                TryGetExprLine(index) ?? 0);
            TypeInfo? current = receiver;
            for (int depth = 0; current is not null && depth < 64; depth++)
            {
                TypeInfo? member = GetStaticMethods(current)?.GetValueOrDefault(name)
                    ?? GetStaticProperties(current)?.GetValueOrDefault(name);
                if (member is not null)
                {
                    EnforceStaticMemberAccess(current, memberName);
                    return member;
                }
                current = GetSuperclass(current);
            }
            return IntrinsicHasInstanceType();
        }

        // Explicit callable object members retain their own signature, including
        // a return type that differs from the inherited intrinsic's boolean.
        bool callable = receiver is TypeInfo.Function or TypeInfo.GenericFunction or
            TypeInfo.OverloadedFunction or TypeInfo.GenericOverloadedFunction or
            TypeInfo.OverloadSet or TypeInfo.ConstructorSignature or
            TypeInfo.Interface { IsCallable: true } or TypeInfo.Interface { IsConstructable: true } or
            TypeInfo.Record { IsCallable: true } or TypeInfo.Record { IsConstructable: true };
        return callable ? GetMemberType(receiver, name) ?? IntrinsicHasInstanceType() : null;
    }

    private static TypeInfo.Function IntrinsicHasInstanceType() =>
        new([TypeInfo.Any.Shared], TypeInfo.Primitive.Boolean);
}
