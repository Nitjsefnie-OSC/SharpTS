using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GenericConstructorCapturedArgumentTests
{
    public const string IssueSource = """
        class Box { next(): Box { return this; } }
        class Derived extends Box {}
        class Factory<T> {
            value: Box;
            constructor(value: Box) { this.value = value; }
            make(): Box { return this.value; }
        }
        const box = new Derived();
        const factory = new Factory<number>(box);
        const expression = (): Box => factory.make();
        const block = (): Box => { return factory.make(); };
        console.log(expression() === box, block() === box, expression() instanceof Derived);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "true true true\n"];
        yield return ["explicit-and-inferred-captured-arguments", """
            class Box { next(): Box { return this; } }
            class Derived extends Box {}
            class Factory<T> {
                value: Box;
                marker: T;
                read: any;
                constructor(value: Box, marker: T) {
                    this.value = value;
                    this.marker = marker;
                    this.read = () => value;
                }
            }
            const box: Box = new Derived();
            const readBox = (): Box => box;
            const makeExplicit = (): any => new Factory<number>(box, 11);
            const makeInferred = (): any => { return new Factory(box, "inferred"); };
            const explicit = makeExplicit(), inferred = makeInferred();
            console.log(explicit.value === box, inferred.value === box,
                explicit.read() === box, inferred.read() === box, explicit.value instanceof Derived);
            console.log(explicit.marker, inferred.marker, readBox() === box);
            """, "true true true true true\n11 inferred true\n"];
        yield return ["mutable-and-shadowed-capture-cells", """
            class Box { next(): Box { return this; } }
            class Derived extends Box {}
            class Factory<T> {
                value: Box;
                constructor(value: Box) { this.value = value; }
            }
            let box: Box = new Derived();
            const initial = box;
            const read = (): Box => box;
            const make = (): any => new Factory<number>(box);
            const first = make();
            box = new Derived();
            const second = make();
            console.log(first.value === initial, second.value === read(),
                first.value !== second.value, second.value instanceof Derived);
            const outer = box;
            const makeShadow = (() => {
                const box: Box = new Derived();
                const read = (): Box => box;
                return (): any => new Factory<string>(read());
            })();
            const shadowFirst = makeShadow(), shadowSecond = makeShadow();
            console.log(shadowFirst.value === shadowSecond.value, shadowFirst.value !== outer,
                read() === outer, shadowFirst.value instanceof Derived);
            """, "true true true true\ntrue true true true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void GenericConstructorsPreserveCapturedClassArguments(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
