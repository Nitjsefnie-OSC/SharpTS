using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

internal sealed record GeneratorExpressionCapture(IReadOnlyList<FieldBuilder> TargetPath, bool IsCell);

/// <summary>Per-output handles for an expression's lexical target and iterator kickoff.</summary>
internal sealed record GeneratorExpressionCompilation(
    Expr.ArrowFunction Source,
    Stmt.Function FunctionShape,
    TypeBuilder TargetType,
    ConstructorBuilder TargetConstructor,
    MethodBuilder Kickoff,
    StateMachineBuilderBase StateMachine,
    FieldBuilder EnvironmentField,
    FieldBuilder FunctionValueField,
    IReadOnlyDictionary<string, GeneratorExpressionCapture> Captures);
