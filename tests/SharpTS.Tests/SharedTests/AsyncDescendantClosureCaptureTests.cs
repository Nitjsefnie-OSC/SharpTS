using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncDescendantClosureCaptureTests
{
    public const string ExactDescendantOnly = """
        async function probe(): Promise<void> {
            const state = { trace: "" };
            function pending(label: string, value: any): Promise<any> {
                return new Promise(resolve => setTimeout(() => { state.trace += label; resolve(value); }, 0));
            }
            console.log(await pending("a", "value"), state.trace);
        }
        probe().then(() => console.log("done"), error => console.log("caught", error));
        """;

    public const string SiblingCallsAndInvocations = """
        async function probe(tag: string): Promise<string> {
            const state: any = { count: 0 };
            function pending(label: string, value: string, fail: boolean): Promise<string> {
                return new Promise((resolve, reject) => setTimeout(() => {
                    state[label] = value;
                    state.count++;
                    if (fail) reject(value); else resolve(value);
                }, 0));
            }
            function read() { return state.count + ':' + state.a + ':' + state.b; }
            const first = pending('a', tag + '-ok', false);
            const second = pending('b', tag + '-bad', true).then(() => 'wrong', reason => 'caught-' + reason);
            const results = await Promise.all([first, second]);
            return results.join('|') + '|' + read();
        }
        async function main() {
            const results = await Promise.all([probe('left'), probe('right')]);
            console.log(results.join(';'));
        }
        main().then(() => console.log('done'), error => console.log('caught', error));
        """;

    public const string HelperOwnedDescendantControl = """
        async function probe(tag: string): Promise<string> {
            function pending(value: string, fail: boolean): Promise<string> {
                return new Promise((resolve, reject) => setTimeout(() => {
                    if (fail) reject(value); else resolve(value);
                }, 0));
            }
            const first = pending(tag + '-ok', false);
            const second = pending(tag + '-bad', true).then(() => 'wrong', reason => 'caught-' + reason);
            return (await Promise.all([first, second])).join('|');
        }
        async function main() {
            const results = await Promise.all([probe('left'), probe('right')]);
            console.log(results.join(';'));
        }
        main().then(() => console.log('done'), error => console.log('caught', error));
        """;

    public const string DirectCaptureLiveControl = """
        async function probe(): Promise<void> {
            const state = { trace: "" };
            function pending(label: string, value: any): Promise<any> {
                void state;
                return new Promise(resolve => setTimeout(() => { state.trace += label; resolve(value); }, 0));
            }
            console.log(await pending("a", "value"), state.trace);
        }
        probe().then(() => console.log("done"), error => console.log("caught", error));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-descendant-only", ExactDescendantOnly, "value a\ndone\n"];
        yield return ["sibling-calls-and-invocations", SiblingCallsAndInvocations, "left-ok|caught-left-bad|2:left-ok:left-bad;right-ok|caught-right-bad|2:right-ok:right-bad\ndone\n"];
        yield return ["helper-owned-descendant-control", HelperOwnedDescendantControl, "left-ok|caught-left-bad;right-ok|caught-right-bad\ndone\n"];
        yield return ["direct-capture-live-control", DirectCaptureLiveControl, "value a\ndone\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DescendantClosuresRetainTheirExternalLexicalOwners(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}