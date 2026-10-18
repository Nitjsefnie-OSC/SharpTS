using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InheritedGenericConstructorTests
{
    public const string OriginalSource = """
        class Base<T> {
            value: T;
            constructor(value: T) { this.value = value; }
        }
        class Middle<U> extends Base<U> {}
        class Child extends Middle<number> {}
        console.log(new Child(7).value);
        """;

    public const string DirectAndExplicitSource = """
        class Base<T> { value: T; constructor(value: T) { this.value = value; } }
        class One extends Base<number> {}
        class ExplicitMiddle<U> extends Base<U> { constructor(value: U) { super(value); } }
        class ExplicitChild extends ExplicitMiddle<number> {}
        class NearestMiddle<U> extends Base<number> { constructor(label: string) { super(label.length); } }
        class NearestChild extends NearestMiddle<boolean> {}
        console.log(new Base<number>(1).value, new One(2).value,
            new ExplicitChild(3).value, new NearestChild("four").value);
        """;

    public const string ComposedPathsSource = """
        class Base<T> { value: T; constructor(value: T) { this.value = value; } }
        class First<A> extends Base<A> {}
        class Middle<B> extends First<B> {}
        class Deep extends Middle<number> {}
        class NestedMiddle<U> extends Base<U[]> {}
        class Nested extends NestedMiddle<number> {}
        class RepeatedMiddle<T> extends Base<T[]> {}
        class Repeated extends RepeatedMiddle<number> {}
        class Pair<A, B> {
            first: A; second: B;
            constructor(first: A, second: B) { this.first = first; this.second = second; }
        }
        class Swap<X, Y> extends Pair<Y, X> {}
        class Reordered extends Swap<string, number> {}
        const deep: number = new Deep(7).value;
        const nested: number = new Nested([8]).value[0];
        const repeated: number = new Repeated([9]).value[0];
        const pair = new Reordered(10, "ten");
        const first: number = pair.first, second: string = pair.second;
        console.log(deep, nested, repeated, first, second);
        """;

    public const string LeafInferenceSource = """
        class Base<T> { value: T; constructor(value: T) { this.value = value; } }
        class Middle<U> extends Base<U> {}
        class Child<V> extends Middle<V> {}
        const explicit = new Child<number>(7);
        const inferred = new Child(8);
        const explicitCheck: number = explicit.value;
        const inferredCheck: number = inferred.value;
        class NestedMiddle<U> extends Base<U[]> {}
        class NestedChild<V> extends NestedMiddle<V> {}
        const nested = new NestedChild([13]);
        const nestedCheck: number = nested.value[0];
        console.log(explicitCheck, inferredCheck, nestedCheck);
        """;

    // Keep the optional parameter while isolating separate field issues:
    // #2449 rejects optional-field undefined assignment; #2555 rejects the
    // undefined sentinel in string | undefined field storage.
    public const string SignatureShapesSource = """
        class OptionalBase<T> {
            value: T; label: any;
            constructor(value: T, label?: string) { this.value = value; this.label = label; }
        }
        class OptionalMiddle<U> extends OptionalBase<U> {}
        class OptionalChild extends OptionalMiddle<number> {}
        const omitted = new OptionalChild(7), supplied = new OptionalChild(8, "label");
        const optionalValue: number = omitted.value;
        const optionalLabel: string | undefined = supplied.label;
        class DefaultBase<T> {
            value: T; label: string;
            constructor(value: T, label: string = "default") { this.value = value; this.label = label; }
        }
        class DefaultMiddle<U> extends DefaultBase<U> {}
        class DefaultChild extends DefaultMiddle<number> {}
        const defaulted = new DefaultChild(9), replaced = new DefaultChild(10, "replacement");
        const defaultLabel: string = defaulted.label, replacedLabel: string = replaced.label;
        class RestBase<T> {
            values: T[];
            constructor(...values: T[]) { this.values = values; }
        }
        class RestMiddle<U> extends RestBase<U> {}
        class RestChild extends RestMiddle<number> {}
        const empty = new RestChild(), rest = new RestChild(11, 12);
        const restValue: number = rest.values[0];
        console.log(optionalValue, optionalLabel, defaultLabel, replacedLabel,
            empty.values.length, rest.values.join(","));
        """;

    public const string SuperSource = """
        class Base<T> { value: T; constructor(value: T) { this.value = value; } }
        class Middle<U> extends Base<U> {}
        class Child extends Middle<number> { constructor(value: number) { super(value); } }
        console.log(new Child(7).value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-two-edge-implicit", OriginalSource, "7\n"];
        yield return ["direct-and-nearest-explicit-constructor", DirectAndExplicitSource, "1 2 3 4\n"];
        yield return ["deep-nested-repeated-and-reordered-paths", ComposedPathsSource, "7 8 9 10 ten\n"];
        yield return ["explicit-and-inferred-generic-leaves", LeafInferenceSource, "7 8 13\n"];
        yield return ["optional-default-and-rest-signatures", SignatureShapesSource, "7 label default replacement 0 11,12\n"];
        yield return ["explicit-leaf-super-through-implicit-middle", SuperSource, "7\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InheritedConstructorsComposeEveryGenericAncestor(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
