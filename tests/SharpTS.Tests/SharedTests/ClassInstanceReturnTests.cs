using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassInstanceReturnTests
{
    public const string IssueSource = """
        class Box { next(): Box { return this; } }
        const box = new Box();
        const next = () => box.next();
        console.log(next() === box);
        """;

    public const string TypedSource = """
        class Box { next(): Box { return this; } }
        class Derived extends Box {}
        const box = new Derived();
        const expression = (): Box => box.next();
        const block = (): Box => { return box.next(); };
        function ordinary(): Box { return box.next(); }
        console.log(expression() === box, block() === box, ordinary() === box,
            expression() instanceof Derived);
        """;

    public const string DeferredSource = """
        class Box { next(): Box { return this; } }
        const box = new Box();
        let completed = 0;
        const next = (): Box => {
            try { return box.next(); }
            finally { completed++; }
        };
        function ordinary(): Box {
            try { return box.next(); }
            finally { completed++; }
        }
        console.log(next() === box, ordinary() === box, completed);
        """;

    public const string GenericSource = """
        class Box { next(): Box { return this; } }
        class Derived extends Box {}
        const box = new Derived();
        class Factory<T> {
            make(value: any): Box { return value; }
        }
        const factory = new Factory<number>();
        const expression = (): Box => factory.make(box);
        const block = (): Box => { return factory.make(box); };
        console.log(expression() === box, block() === box, expression() instanceof Derived);
        """;

    public const string StringSource = """
        const expression = (value: any) => value ? "present" : undefined;
        const block = (value: any) => { return value ? "present" : undefined; };
        console.log(expression(true), block(true), expression(false) === undefined,
            block(false) === undefined);
        """;

    public const string ConstructorSource = """
        class C {}
        const expression = () => C;
        const block = () => { return C; };
        function ordinary() { return C; }
        function deferred() { try { return C; } finally {} }
        const Local = class {};
        const local = () => Local;
        console.log(expression() === C, block() === C, ordinary() === C,
            deferred() === C, local() === Local);
        """;

    public const string UndefinedSource = """
        class Box { value: number = 7; }
        const expression = (): Box => undefined as any;
        const block = (): Box => { return undefined as any; };
        let completed = 0;
        const deferred = (): Box => {
            try { return undefined as any; }
            finally { completed++; }
        };
        function ordinary(): Box { return undefined as any; }
        const box = new Box();
        const normal = (): Box => box;
        console.log(expression() === undefined, block() === undefined,
            deferred() === undefined, ordinary() === undefined, completed);
        console.log(normal() === box, normal() instanceof Box, normal().value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true\n"];
        yield return ["typed", TypedSource, "true true true true\n"];
        yield return ["deferred", DeferredSource, "true true 2\n"];
        yield return ["generic", GenericSource, "true true true\n"];
        yield return ["string", StringSource, "present present true true\n"];
        yield return ["constructors", ConstructorSource, "true true true true true\n"];
        yield return ["undefined", UndefinedSource, "true true true true 1\ntrue true 7\n"];
    }

    [Theory, MemberData(nameof(Cases))]
    public void InterpreterPreservesReturnValues(string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunInterpreted(source));
    }
}
