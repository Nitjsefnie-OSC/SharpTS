using SharpTS.Parsing;
using SharpTS.TypeSystem.Exceptions;

namespace SharpTS.TypeSystem;

public partial class TypeChecker
{
    private void CheckPrivateCallArguments(Expr.CallPrivate call, TypeInfo.Function signature)
    {
        int regularCount = signature.ParamTypes.Count - (signature.HasRestParam ? 1 : 0);
        TypeInfo? restElement = signature.HasRestParam && signature.ParamTypes.Count > 0
            ? signature.ParamTypes[^1] is TypeInfo.Array array ? array.ElementType : signature.ParamTypes[^1]
            : null;
        int parameterIndex = 0;
        int guaranteedCount = 0;
        bool looseVariadic = restElement is TypeInfo.Any &&
            signature.ParamTypes.Take(regularCount).All(type => type is TypeInfo.Any);
        bool hasLooseSpread = false;

        foreach (var argument in call.Arguments)
        {
            var type = CheckExpr(argument);
            if (argument is not Expr.Spread)
            {
                CheckElement(type, optionalElement: false);
            }
            else
            {
                CheckIterable(type);
            }
        }

        if (!hasLooseSpread && guaranteedCount < signature.MinArity)
            throw new TypeCheckException(
                $" Private method '{call.Name.Lexeme}' requires at least {signature.MinArity} arguments, got {guaranteedCount}.",
                tsCode: "TS2554");

        void CheckElement(TypeInfo type, bool optionalElement)
        {
            if (parameterIndex >= regularCount && restElement == null)
                throw new TypeCheckException(
                    $" Private method '{call.Name.Lexeme}' accepts at most {signature.ParamTypes.Count} arguments, got at least {parameterIndex + 1}.",
                    tsCode: "TS2554");

            TypeInfo expected = parameterIndex < regularCount
                ? signature.ParamTypes[parameterIndex] : restElement!;
            bool optionalParameter = parameterIndex < regularCount && parameterIndex >= signature.MinArity;
            if (optionalElement)
                type = new TypeInfo.Union([type, TypeInfo.Undefined.Shared]);
            if (!IsArgumentCompatible(expected, type, optionalParameter))
                throw new TypeCheckException(
                    $" Argument {parameterIndex + 1} to private method '{call.Name.Lexeme}' has type '{type}' but expected '{expected}'.",
                    tsCode: "TS2345");

            parameterIndex++;
            if (!optionalElement) guaranteedCount++;
        }

        void CheckIterable(TypeInfo type)
        {
            if (type is TypeInfo.Tuple tuple)
            {
                foreach (var tupleElement in tuple.Elements)
                {
                    if (tupleElement.IsSpread) CheckIterable(tupleElement.Type);
                    else CheckElement(tupleElement.Type, tupleElement.IsOptional);
                }
                if (tuple.RestElementType is { } trailingRest)
                    CheckRestElement(trailingRest);
                return;
            }
            if (!TryGetSpreadElementType(type, out var element))
                throw new TypeCheckException(
                    $"Spread argument must be an iterable type (array, iterator, set, map, string, or generator), got '{type}'.",
                    tsCode: "TS2488");
            CheckRestElement(element);
        }

        void CheckRestElement(TypeInfo element)
        {
            // Preserve the loose JS calling convention for an entirely any-typed
            // variadic signature, including a spread that supplies its fixed prefix.
            if (looseVariadic)
            {
                parameterIndex = Math.Max(parameterIndex, regularCount);
                hasLooseSpread = true;
            }
            // An unknown-length iterable cannot supply fixed positional parameters.
            // Fixed tuple elements above consume those positions individually.
            if (restElement == null || parameterIndex < regularCount)
                throw new TypeCheckException(
                    "A spread argument must either have a tuple type or be passed to a rest parameter.",
                    tsCode: "TS2556");
            if (!IsCompatible(restElement, element))
                throw new TypeCheckException(
                    $"Spread element type '{element}' not compatible with rest parameter type '{restElement}'.",
                    tsCode: "TS2345");
        }
    }
}
