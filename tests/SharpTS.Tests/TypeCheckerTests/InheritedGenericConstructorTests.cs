using SharpTS.Diagnostics;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class InheritedGenericConstructorTests
{
    // Rejected sources are checked only; an unresolved ancestor parameter is not
    // evidence that the concrete argument contract was enforced.
    public static IEnumerable<object[]> RejectedCases()
    {
        yield return ["wrong-concrete-original", """
            class Base<T> {
                value: T;
                constructor(value: T) { this.value = value; }
            }
            class Middle<U> extends Base<U> {}
            class Child extends Middle<number> {}
            console.log(new Child("bad").value);
            """, "TS2345", 7, "number"];

        yield return ["wrong-nested-array", """
            class Base<T> { constructor(value: T) {} }
            class Middle<U> extends Base<U[]> {}
            class Child extends Middle<number> {}
            const wrong: string[] = ["bad"];
            new Child(wrong);
            """, "TS2345", 5, "number[]"];

        yield return ["wrong-reordered-argument", """
            class Pair<A, B> { constructor(first: A, second: B) {} }
            class Swap<X, Y> extends Pair<Y, X> {}
            class Child extends Swap<string, number> {}
            new Child("bad", "seven");
            """, "TS2345", 4, "number"];

        yield return ["wrong-explicit-generic-leaf", """
            class Base<T> { constructor(value: T) {} }
            class Middle<U> extends Base<U> {}
            class Child<V> extends Middle<V> {}
            new Child<string>(7);
            """, "TS2345", 4, "string"];

        yield return ["unrelated-function-parameter-cannot-match-ancestor-name", """
            class Base<T> { constructor(value: T) {} }
            class Middle<U> extends Base<U> {}
            class Child extends Middle<number> {}
            function make<U>(value: U) { return new Child(value); }
            """, "TS2345", 4, "number"];

        yield return ["missing-required-argument", """
            class Base<T> { constructor(value: T) {} }
            class Middle<U> extends Base<U> {}
            class Child extends Middle<number> {}
            new Child();
            """, "TS2554", 4, ""];

        yield return ["surplus-argument", """
            class Base<T> { constructor(value: T) {} }
            class Middle<U> extends Base<U> {}
            class Child extends Middle<number> {}
            new Child(7, 8);
            """, "TS2554", 4, ""];

        yield return ["expression-owned-parameter-masks-outer-alias-negative", """
            type U = string;
            class Base<T> { constructor(value: T) {} }
            const Middle = class<U> extends Base<U> {};
            class Child extends Middle<number> {}
            new Child("wrong");
            """, "TS2345", 5, "number"];

        yield return ["wrong-super-through-implicit-middle", """
            class Base<T> { constructor(value: T) {} }
            class Middle<U> extends Base<U> {}
            class Child extends Middle<number> {
                constructor(value: number) { super("wrong"); }
            }
            new Child(7);
            """, "TS2345", 4, "number"];

        yield return ["nearest-explicit-constructor-keeps-its-signature", """
            class Base<T> { constructor(value: T) {} }
            class Middle<U> extends Base<number> { constructor(label: string) { super(label.length); } }
            class Child extends Middle<boolean> {}
            new Child(7);
            """, "TS2345", 4, "string"];
    }

    [Theory, MemberData(nameof(RejectedCases))]
    public void InheritedConstructorsRejectAgainstConcreteParametersAndArity(
        string name, string source, string expectedCode, int expectedLine, string expectedParameter)
    {
        Assert.NotEmpty(name);
        var diagnostic = Assert.Single(Errors(source));
        Assert.Equal(expectedCode, diagnostic.TsCode);
        Assert.Equal(expectedLine, diagnostic.Line);
        if (expectedParameter.Length > 0) Assert.Contains(expectedParameter, diagnostic.Message);
    }

    [Fact]
    public void GenericExpressionHeritageUsesItsOwnTypeParameterBeforeAnOuterAlias()
    {
        // This checker-only scope pair does not assert the separate #2332
        // declaration/expression/declaration module runtime chain.
        const string source = """
            type U = string;
            class Base<T> { constructor(value: T) {} }
            const Middle = class<U> extends Base<U> {};
            class Child extends Middle<number> {}
            new Child(7);
            """;
        Assert.Empty(Errors(source));
    }

    // #2448: field reads also compose ancestor bindings without constructor arguments.
    public static IEnumerable<object[]> FieldResultCases()
    {
        yield return ["unrelated-function-parameter-cannot-match-field-ancestor-name", """
            class Base<T> { value: T = undefined as any; }
            class Middle<U> extends Base<U> {}
            class Child extends Middle<number> {}
            function read<U>(value: Child): U { return value.value; }
            """, 4, "number"];

        yield return ["generic-leaf-field-result-is-concrete", """
            class Base<T> { value: T = undefined as any; }
            class Middle<U> extends Base<U> {}
            class Child<V> extends Middle<V> {}
            function read(value: Child<number>): string { return value.value; }
            """, 4, "number"];

        yield return ["nested-field-result-is-concrete", """
            class Base<T> { value: T = undefined as any; }
            class Middle<U> extends Base<U[]> {}
            class Child extends Middle<number> {}
            function read(value: Child): string[] { return value.value; }
            """, 4, "number[]"];
    }

    [Theory, MemberData(nameof(FieldResultCases))]
    public void InheritedFieldResultsCannotUseUnboundOrUnrelatedTypeParameters(
        string name, string source, int expectedLine, string concreteType)
    {
        Assert.NotEmpty(name);
        var diagnostic = Assert.Single(Errors(source));
        Assert.Equal("TS2322", diagnostic.TsCode);
        Assert.Equal(expectedLine, diagnostic.Line);
        Assert.Contains(concreteType, diagnostic.Message);
    }

    private static Diagnostic[] Errors(string source)
    {
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        return new TypeChecker(strictNullChecks: true, maxErrors: 20).CheckWithRecovery(statements)
            .Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
    }
}
