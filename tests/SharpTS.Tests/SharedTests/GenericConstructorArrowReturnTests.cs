using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GenericConstructorArrowReturnTests
{
    public const string IssueSource = """
        class Generic<T> {}
        const get = () => Generic;
        console.log(get() === Generic);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "true\n"];
        yield return ["nongeneric-control", """
            class Plain {}
            const get = () => Plain;
            console.log(get() === Plain);
            """, "true\n"];
        yield return ["return-and-capture-forms", """
            class Generic<T> {}
            const block = () => { return Generic; };
            function ordinary() { return Generic; }
            const nested = () => () => Generic;
            const Alias = Generic;
            const alias = () => Alias;
            const typed = (): typeof Generic => Generic;
            console.log(block() === Generic, ordinary() === Generic, nested()() === Generic,
                alias() === Generic, typed() === Generic);
            """, "true true true true true\n"];
        yield return ["live-mutable-constructor-cell", """
            class Generic<T> {}
            class Other<U> {}
            let selected: any = Generic;
            const read = () => selected;
            console.log(read() === Generic);
            selected = Other;
            console.log(read() === Other);
            """, "true\ntrue\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ArrowReturnsPreserveGenericConstructorValues(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
