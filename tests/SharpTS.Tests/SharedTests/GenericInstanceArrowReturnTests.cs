using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GenericInstanceArrowReturnTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["expression-block-explicit", """
            class Factory<T> {}
            const expression = () => new Factory<number>();
            const block = () => { return new Factory<string>(); };
            const typed = (): Factory<number> => new Factory<number>();
            console.log(expression() instanceof Factory, block() instanceof Factory,
                typed() instanceof Factory);
            """, "true true true\n"];
        yield return ["captured-callback", """
            class Factory<T> {}
            const instance = new Factory<number>();
            const captured = () => instance;
            const values = [1].map(() => new Factory<string>());
            console.log(captured() === instance, values[0] instanceof Factory);
            """, "true true\n"];
        yield return ["generic-owners", """
            class Factory<T> {}
            class MethodOwner {
                make<U>() {
                    const create = () => new Factory<U>();
                    return create();
                }
            }
            class ClassOwner<U> {
                make() {
                    const create = () => new Factory<U>();
                    return create();
                }
            }
            console.log(new MethodOwner().make<number>() instanceof Factory,
                new ClassOwner<string>().make() instanceof Factory);
            """, "true true\n"];
        yield return ["return-controls", """
            class Factory<T> {}
            class Plain {}
            class Holder<T> {
                accessor value: T;
                constructor(value: T) { this.value = value; }
            }
            const anyReturn = (): any => new Factory<number>();
            const plain = () => new Plain();
            const constructor = () => Factory;
            function ordinary(): Factory<string> { return new Factory<string>(); }
            const asyncReturn = async () => new Factory<number>();
            console.log(anyReturn() instanceof Factory, plain() instanceof Plain,
                constructor() === Factory, ordinary() instanceof Factory);
            const number = new Holder<number>(7), text = new Holder<string>("text");
            console.log(number.value, text.value);
            async function run(): Promise<void> {
                console.log((await asyncReturn()) instanceof Factory);
            }
            run();
            """, "true true true true\n7 text\ntrue\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ArrowReturnsPreserveGenericInstances(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
