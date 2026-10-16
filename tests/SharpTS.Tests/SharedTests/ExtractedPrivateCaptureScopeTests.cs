using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ExtractedPrivateCaptureScopeTests
{
    public const string IssueSource = """
        const value = 90;
        function make(value: number) {
            return class C {
                #m() { return value; }
                read() { return this.#m(); }
                extract() { return this.#m; }
            };
        }
        const C = make(3), instance = new C(), read = instance.extract();
        console.log(instance.read(), read.call(instance), read.apply(instance, []), read.bind(instance)(), value);
        """;

    public const string LiveCaptureAndSuperSource = """
        const value = 90;
        class Base { read() { return 10; } }
        function make(value: number) {
            const C = class C extends Base {
                #captured() { return value; }
                #withSuper() { return value + super.read(); }
                extract() { return this.#captured; }
                extractSuper() { return this.#withSuper; }
            };
            return { C, set(next: number) { value = next; } };
        }
        const left = make(3), right = make(7);
        const Left = left.C, Right = right.C;
        const first = new Left(), second = new Right();
        const read = first.extract(), other = second.extract(), readSuper = first.extractSuper();
        console.log(read === first.extract(), read === other);
        console.log(read.call(second), read.apply({}, []), read.bind(null)(), read.call(undefined));
        left.set(5);
        right.set(9);
        console.log(read.call(null), other.call(first), readSuper.call(second), value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "3 3 3 3 90\n"];
        yield return ["live-super", LiveCaptureAndSuperSource, "true false\n3 3 3 3\n5 9 15 90\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ExtractedPrivateMethodsKeepTheirLexicalCaptureScope(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
