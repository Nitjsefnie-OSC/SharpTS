using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ZeroParameterArrayCallbackTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["empty-for-each", """
            [1].forEach(() => {});
            console.log('done');
            """, "done\n"];
        yield return ["string-throw", """
            try { [1].forEach(() => { throw 'TypeError: in-forEach'; }); }
            catch (error: any) {
                console.log(typeof error, error instanceof Error);
                console.log(error);
            }
            """, "string false\nTypeError: in-forEach\n"];
        yield return ["error-throw", """
            const token = new RangeError('boom');
            try { [1].forEach(() => { throw token; }); }
            catch (error: any) {
                console.log(typeof error, error instanceof Error, error instanceof RangeError, error === token);
                console.log(error.message);
            }
            """, "object true true true\nboom\n"];
        yield return ["captured-callback-value", """
            const state = { calls: 0 };
            const callback = () => { state.calls++; };
            [1, 2, 3].forEach(callback);
            console.log(state.calls);
            """, "3\n"];
        yield return ["function-expression", """
            const state = { calls: 0 };
            [1, 2].forEach(function() { state.calls++; });
            console.log(state.calls);
            """, "2\n"];
        yield return ["local-display-class", """
            function run(start: number) {
                let count = start;
                [1, 2].forEach(() => { count++; });
                return count;
            }
            console.log(run(3), run(8));
            """, "5 10\n"];
        yield return ["related-array-callbacks", """
            console.log([1, 2].map(() => 7).join(','), [1, 2].filter(() => true).join(','));
            console.log([1, 2].find(() => true), [1, 2].some(() => true), [1, 2].every(() => true));
            """, "7,7 1,2\n1 true true\n"];
        yield return ["one-parameter-control", """
            const state = { sum: 0 };
            [1, 2, 3].forEach((value: any) => { state.sum += value; });
            console.log(state.sum);
            """, "6\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void CallbacksVerifyBeforeExecution(string name, string source, string expected, ExecutionMode mode)
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
