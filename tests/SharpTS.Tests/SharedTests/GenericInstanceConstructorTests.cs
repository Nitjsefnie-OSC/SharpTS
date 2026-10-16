using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GenericInstanceConstructorTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-promise", """
            class MyPromise<T> extends Promise<T> {}
            async function main() {
                const p = MyPromise.resolve(1);
                console.log(p.constructor === MyPromise);
                console.log(p.constructor === Promise);
                const q = Promise.resolve(1);
                console.log(q.constructor === Promise);
            }
            main();
            """, "true\nfalse\ntrue\n"];
        yield return ["original-controls", """
            class PlainPromise extends Promise<number> {}
            class GenericPromise<T> extends Promise<T> {}
            class Box<T> {}
            const plain: any = PlainPromise.resolve(1);
            const constructed: any = new GenericPromise<number>((resolve: any) => resolve(2));
            const boxed: any = new Box<number>();
            console.log(plain.constructor === PlainPromise);
            console.log(constructed.constructor === GenericPromise);
            console.log(boxed.constructor === Box);
            """, "true\ntrue\ntrue\n"];
        yield return ["promise-subclass-prototype", """
            class PlainPromise extends Promise<number> {}
            class GenericPromise<T> extends Promise<T> {}
            console.log(PlainPromise.prototype.constructor === PlainPromise);
            console.log(GenericPromise.prototype.constructor === GenericPromise);
            """, "true\ntrue\n"];
        yield return ["erased-instantiations", """
            class Box<T> { value: T; constructor(value: T) { this.value = value; } }
            const numberBox: any = new Box<number>(7);
            const stringBox: any = new Box<string>('text');
            console.log(numberBox.constructor === Box, stringBox.constructor === Box);
            console.log(numberBox.constructor === stringBox.constructor);
            console.log(numberBox['constructor'] === Box, Reflect.get(numberBox, 'constructor') === Box);
            console.log((Box as any).prototype.constructor === Box);
            console.log(numberBox.value, stringBox.value);
            """, "true true\ntrue\ntrue true\ntrue\n7 text\n"];
        yield return ["multiple-parameters-and-inheritance", """
            class Base<T> {}
            class Derived<T, U> extends Base<T> {}
            const first: any = new Derived<number, string>();
            const second: any = new Derived<string, number>();
            console.log(first.constructor === Derived, second.constructor === Derived);
            console.log(first.constructor !== Base, first instanceof Base, first instanceof Derived);
            console.log((Base as any).prototype.constructor === Base, (Derived as any).prototype.constructor === Derived);
            """, "true true\ntrue true true\ntrue true\n"];
        yield return ["named-expression", """
            const Owner = class Named<T> {};
            const first: any = new Owner<number>();
            const second: any = new Owner<string>();
            console.log(first.constructor === Owner, second.constructor === Owner);
            console.log((Owner as any).prototype.constructor === Owner);
            """, "true true\ntrue\n"];
        yield return ["separate-evaluations", """
            function make() { return class Named<T> {}; }
            const First: any = make(), Second: any = make();
            const first: any = new First(), second: any = new Second();
            console.log(First !== Second, First.prototype !== Second.prototype);
            console.log(first.constructor === First, second.constructor === Second);
            console.log(first.constructor !== second.constructor);
            """, "true true\ntrue true\ntrue\n"];
        yield return ["own-constructor-precedence", """
            class Box<T> {}
            const first: any = new Box<number>(), second: any = new Box<string>();
            first.constructor = 7;
            console.log(first.constructor, Reflect.get(first, 'constructor'), second.constructor === Box);
            Object.defineProperty(first, 'constructor', { get() { return 'custom'; }, configurable: true });
            console.log(first.constructor, second.constructor === Box);
            """, "7 7 true\ncustom true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void InstancesExposeTheirCanonicalGuestConstructor(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
