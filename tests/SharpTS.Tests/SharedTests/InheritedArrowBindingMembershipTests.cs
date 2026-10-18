using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InheritedArrowBindingMembershipTests
{
    public const string Exact = """
        function outer() {
            function middle() {
                let count = 10;
                const marker = "scope";
                const read = () => count;
                const local = () => {
                    let count = 200;
                    console.log(marker, count);
                    count = count + 1;
                    console.log(count);
                };
                local();
                console.log(read(), count);
            }
            middle();
        }
        outer();
        """;

    public const string FunctionOwner = """
        function outer() {
            let count = 10;
            const marker = "scope";
            const read = () => count;
            const local = () => {
                let count = 200;
                console.log(marker, count);
                count = count + 1;
                console.log(count);
            };
            local();
            console.log(read(), count);
        }
        outer();
        """;

    public const string EscapedSiblingStorage = """
        function outer() {
            function create(marker: string) {
                let count = 10;
                const read = () => count;
                const add = () => { count = count + 2; return count; };
                const local = () => {
                    let count = 200;
                    count = count + 1;
                    return marker + ':' + count;
                };
                return { read, add, local };
            }
            const first = create('first'), second = create('second');
            console.log(first.local(), first.read(), second.read());
            console.log(second.add(), second.local(), first.local());
            console.log(first.read(), second.read());
        }
        outer();
        """;

    public const string OwnScopeControl = """
        function outer() {
            function middle() {
                let count = 10;
                const marker = 'scope';
                const parentRead = () => count;
                const local = () => {
                    let count = 200;
                    const ownRead = () => count;
                    count = count + 1;
                    console.log(marker, ownRead(), count);
                };
                local();
                console.log(parentRead(), count);
            }
            middle();
        }
        outer();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact", Exact, "scope 200\n201\n10 10\n"];
        yield return ["function-owner", FunctionOwner, "scope 200\n201\n10 10\n"];
        yield return ["escaped-sibling-storage", EscapedSiblingStorage, "first:201 10 10\n12 second:201 first:201\n10 12\n"];
        yield return ["own-scope-control", OwnScopeControl, "scope 201 201\n10 10\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InheritedStorageMembershipPreservesTheActualLexicalBinding(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
