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
            else if (looseVariadic || !CheckInlineArray(((Expr.Spread)argument).Expression))
            {
                CheckIterable(type);
            }
        }

        if (!hasLooseSpread && guaranteedCount < signature.MinArity)
            throw new TypeCheckException(
                $" Private method '{call.Name.Lexeme}' requires at least {signature.MinArity} arguments, got {guaranteedCount}.",
                line: call.Name.Line, tsCode: "TS2554");

        void CheckElement(TypeInfo type, bool optionalElement)
        {
            if (parameterIndex >= regularCount && restElement == null)
                throw new TypeCheckException(
                    $" Private method '{call.Name.Lexeme}' accepts at most {signature.ParamTypes.Count} arguments, got at least {parameterIndex + 1}.",
                    line: call.Name.Line, tsCode: "TS2554");

            TypeInfo expected = parameterIndex < regularCount
                ? signature.ParamTypes[parameterIndex] : restElement!;
            bool optionalParameter = parameterIndex < regularCount && parameterIndex >= signature.MinArity;
            if (optionalElement)
                type = new TypeInfo.Union([type, TypeInfo.Undefined.Shared]);
            if (!IsArgumentCompatible(expected, type, optionalParameter))
                throw new TypeCheckException(
                    $" Argument {parameterIndex + 1} to private method '{call.Name.Lexeme}' has type '{type}' but expected '{expected}'.",
                    line: call.Name.Line, tsCode: "TS2345");

            parameterIndex++;
            if (!optionalElement) guaranteedCount++;
        }

        bool CheckInlineArray(Expr expression)
        {
            while (expression is Expr.Grouping grouping) expression = grouping.Expression;
            if (expression is not Expr.ArrayLiteral literal) return false;

            // CheckExpr above visited every child, including nested spread operands.
            // Retain their positional types without checking the initializer twice or
            // treating an asserted array as if it had a fixed tuple length.
            foreach (var element in literal.Elements)
            {
                if (element is Expr.Spread spread)
                {
                    // Only the outer literal receives positional call context.
                    // Nested spread operands keep their tuple or unknown-length type.
                    CheckIterable(CheckedType(spread.Expression));
                }
                else
                {
                    // Elisions are represented by checked undefined literals and
                    // consume an argument just like an explicit undefined element.
                    CheckElement(CheckedType(element), optionalElement: false);
                }
            }
            return true;
        }

        TypeInfo CheckedType(Expr expression) => _typeMap.Get(expression)
            ?? throw new InvalidOperationException("Private call spread element was not checked.");

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
                    line: call.Name.Line, tsCode: "TS2488");
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
                    line: call.Name.Line, tsCode: "TS2556");
            if (!IsCompatible(restElement, element))
                throw new TypeCheckException(
                    $"Spread element type '{element}' not compatible with rest parameter type '{restElement}'.",
                    line: call.Name.Line, tsCode: "TS2345");
        }
    }
}
