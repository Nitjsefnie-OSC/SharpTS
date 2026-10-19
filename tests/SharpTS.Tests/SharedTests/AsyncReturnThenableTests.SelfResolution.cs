using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed partial class AsyncReturnThenableTests
{
    public static IEnumerable<object[]> SelfResolutionCases()
    {
        yield return ["returned-thenable-self-cycle", """
            function start() {
                let pending: any;
                const value: any = { then(resolve: any) { resolve(pending); } };
                async function answer(): Promise<any> { return value; }
                pending = answer();
                pending.catch((error: any) => console.log(error.name));
                setTimeout(() => console.log('done'), 1);
            }
            start();
            """, "TypeError\ndone\n"];
        yield return ["nested-self-cycle-callable-forms", """
            function start(kind: number) {
                let pending: any;
                const value: any = { then(resolve: any) {
                    resolve({ then(again: any) { again(pending); } });
                } };
                async function declared(): Promise<any> { return value; }
                const ordinary = async function(): Promise<any> { return value; };
                const block = async (): Promise<any> => { return value; };
                const expression = async (): Promise<any> => value;
                const forms = [declared, ordinary, block, expression];
                const selected = forms[kind];
                pending = selected();
                return pending.then(() => false, (error: any) => error instanceof TypeError);
            }
            const checks: any[] = [];
            for (let kind = 0; kind < 4; kind++) checks.push(start(kind));
            Promise.all(checks).then(results => console.log(results.length, results.every(value => value)));
            """, "4 true\n"];
        yield return ["independent-invocations-and-first-settlement", """
            function start() {
                const marker = {}, state: any = { promises: [] };
                async function answer(index: number): Promise<any> {
                    return { then(resolve: any, reject: any) {
                        if (index === 0) {
                            resolve({ then(again: any) { again(state.promises[0]); } });
                            reject(marker); throw marker;
                        }
                        resolve(47); resolve(state.promises[1]); reject(marker);
                    } };
                }
                state.promises[0] = answer(0);
                state.promises[1] = answer(1);
                const rejected = state.promises[0].then(() => false, (error: any) => error instanceof TypeError);
                Promise.all([rejected, state.promises[1]]).then(results => console.log(results[0], results[1]));
            }
            start();
            """, "true 47\n"];
        yield return ["self-cycle-before-poisoned-then-read", """
            function start() {
                const marker = {}, state = { reads: 0 };
                let pending: any;
                const value: any = { then(resolve: any) { resolve(pending); } };
                async function answer(): Promise<any> { return value; }
                pending = answer();
                Object.defineProperty(pending, 'then', { get() { state.reads++; throw marker; } });
                const prototype: any = Promise.prototype;
                prototype.then.call(pending, undefined, (error: any) => {
                    console.log(error instanceof TypeError, state.reads);
                });
            }
            start();
            """, "true 0\n"];
    }

    // Compiled returned thenables separately miss the public promise cycle (#2578).
    [Theory, MemberData(nameof(SelfResolutionCases))]
    public void ReturnedThenableCyclesRejectThePublicPromise(string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, ExecutionMode.Interpreted));
    }
}
