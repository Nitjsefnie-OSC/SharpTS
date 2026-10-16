using SharpTS.Parsing;

namespace SharpTS.Compilation;

public partial class CompilationContext
{
    internal IReadOnlyDictionary<Expr.ArrowFunction, GeneratorExpressionCompilation> GeneratorExpressions { get; set; }
        = new Dictionary<Expr.ArrowFunction, GeneratorExpressionCompilation>(ReferenceEqualityComparer.Instance);

    internal GeneratorExpressionCompilation? CurrentGeneratorExpression { get; set; }
}
