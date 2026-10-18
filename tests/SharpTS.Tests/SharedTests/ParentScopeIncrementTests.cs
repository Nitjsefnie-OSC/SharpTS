using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ParentScopeIncrementTests
{
    public const string IssueSource = """
        function outer() {
            function middle() {
                let count = 10;
                function next() { return ++count; }
                console.log(next(), count);
            }
            middle();
        }
        outer();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "11 11\n"];
        yield return ["local-expression-and-discarded", """
            function outer() {
                function middle() {
                    let count = 10;
                    function expressions() {
                        console.log(++count, count++, --count, count--, count);
                    }
                    function increment() { ++count; count++; }
                    function decrement() { --count; count--; }
                    function read() { return count; }
                    expressions();
                    console.log(read(), count);
                    increment();
                    console.log(read(), count);
                    decrement();
                    console.log(read(), count);
                }
                middle();
            }
            outer();
            """, "11 11 11 11 10\n10 10\n12 12\n10 10\n"];
        yield return ["parameter", """
            function outer() {
                function middle(count: number) {
                    function expressions() {
                        console.log(++count, count++, --count, count--, count);
                    }
                    function increment() { ++count; count++; }
                    function decrement() { --count; count--; }
                    function read() { return count; }
                    expressions();
                    increment();
                    console.log(read());
                    decrement();
                    console.log(read());
                }
                middle(4);
            }
            outer();
            """, "5 5 5 5 4\n6\n4\n"];
        yield return ["arrow-parameter", """
            function outer() {
                const middle = (count: number) => {
                    const expressions = () => console.log(++count, count++, --count, count--, count);
                    const increment = () => { ++count; count++; };
                    const decrement = () => { --count; count--; };
                    const read = () => count;
                    expressions();
                    increment();
                    console.log(read());
                    decrement();
                    console.log(read());
                };
                middle(20);
            }
            outer();
            """, "21 21 21 21 20\n22\n20\n"];
        yield return ["multiple-ancestor-scopes", """
            function outer() {
                function first() {
                    let far = 100;
                    function middle() {
                        let near = 10;
                        console.log(far);
                        function expressions() {
                            console.log(++near, far++, --near, far--);
                        }
                        function increment() { ++near; far++; }
                        function decrement() { --near; far--; }
                        function read() { return near + ":" + far; }
                        expressions();
                        console.log(read());
                        increment();
                        console.log(read());
                        decrement();
                        console.log(read());
                    }
                    middle();
                    console.log(far);
                }
                first();
            }
            outer();
            """, "100\n11 100 10 101\n10:100\n11:101\n10:100\n100\n"];
        yield return ["shadow-isolation", """
            function outer() {
                let count = 1;
                const read = () => count;
                function middle() {
                    let count = 10;
                    function next() { return ++count; }
                    function previous() { return count--; }
                    function read() { return count; }
                    console.log(next(), read(), previous(), read(), count);
                }
                middle();
                console.log(read(), count);
            }
            outer();
            """, "11 11 11 10 10\n1 1\n"];
        yield return ["block-local-shadows-captured-binding", """
            function outer() {
                function middle() {
                    let count = 10;
                    function update() {
                        console.log(count);
                        {
                            let count = 100;
                            console.log(++count, count++, --count, count--, count);
                        }
                        console.log(++count, count);
                    }
                    update();
                    console.log(count);
                }
                middle();
            }
            outer();
            """, "10\n101 101 101 101 100\n11 11\n11\n"];
        yield return ["sibling-live-reads-and-invocation-isolation", """
            function outer() {
                function middle(initial: number) {
                    let count = initial;
                    function next() { return ++count; }
                    function previous() { return count--; }
                    function read() { return count; }
                    return { next, previous, read };
                }
                return middle;
            }
            const make = outer();
            const left = make(10), right = make(100);
            console.log(left.next(), left.read(), right.read());
            console.log(right.previous(), right.read(), left.read());
            console.log(left.previous(), left.read(), right.next(), right.read());
            """, "11 11 100\n100 99 11\n11 10 100 100\n"];
    }

    [Fact]
    public void ArrowShadowsParentScopeField_Interpreter()
    {
        // Compiled coverage is pending the separate parent-scope shadow resolution issue,
        // which also affects plain assignments and reads without increment operators.
        const string source = """
            function outer() {
                function middle() {
                    let count = 10;
                    const marker = "scope";
                    const read = () => count;
                    const parameter = (count: number) => {
                        console.log(marker, ++count, count);
                    };
                    const local = () => {
                        let count = 200;
                        console.log(marker, count++, count);
                    };
                    parameter(100);
                    local();
                    console.log(read(), count);
                }
                middle();
            }
            outer();
            """;
        Assert.Equal("scope 101 101\nscope 200 201\n10 10\n",
            TestHarness.Run(source, ExecutionMode.Interpreted));
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void IncrementsAndDecrementsUpdateLiveParentScopeBindings(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
