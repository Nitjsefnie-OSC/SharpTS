using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticStateMachineReceiverTests
{
    public const string IssueSource = """
        class C {
            static async who() { return this; }
            static *generator() { yield this; }
            static async *asyncGenerator() { yield this; }
        }
        async function main() {
            console.log((await C.who()) === C);
            console.log(C.generator().next().value === C);
            console.log((await C.asyncGenerator().next()).value === C);
        }
        main();
        """;

    public const string InterleavedSource = """
        let release: any;
        const gate = new Promise<any>((resolve: any) => { release = resolve; });
        class Base {
            static async asynchronous(gate: any) { await gate; return this; }
            static *generator() { yield "pause"; yield this; }
            static async *asyncGenerator(gate: any) {
                await gate;
                yield this;
                await Promise.resolve(0);
                yield this;
            }
        }
        class Derived extends Base {}
        const base: any = Base, derived: any = Derived;
        const copied: any = {
            asynchronous: base.asynchronous,
            generator: base.generator,
            asyncGenerator: base.asyncGenerator
        };
        async function main() {
            const one = base.asynchronous(gate), two = derived.asynchronous(gate);
            const three = copied.asynchronous(gate);
            const syncOne = base.generator(), syncTwo = derived.generator(), syncThree = copied.generator();
            console.log(syncOne.next().value, syncTwo.next().value, syncThree.next().value);
            const asyncOne = base.asyncGenerator(gate), asyncTwo = derived.asyncGenerator(gate);
            const asyncThree = copied.asyncGenerator(gate);
            const nextOne = asyncOne.next(), nextTwo = asyncTwo.next(), nextThree = asyncThree.next();
            release(0);
            console.log((await one) === base, (await two) === derived, (await three) === copied);
            console.log(syncOne.next().value === base, syncTwo.next().value === derived, syncThree.next().value === copied);
            console.log((await nextOne).value === base, (await nextTwo).value === derived, (await nextThree).value === copied);
            console.log((await asyncOne.next()).value === base, (await asyncTwo.next()).value === derived, (await asyncThree.next()).value === copied);
        }
        main();
        """;

    public const string InterleavedOutput = "pause pause pause\ntrue true true\ntrue true true\ntrue true true\ntrue true true\n";

    [Theory, ModeData]
    public void DeclaredStaticStateMachinesRetainOwnReceiver(ExecutionMode mode)
        => Assert.Equal("true\ntrue\ntrue\n", TestHarness.Run(IssueSource, mode));

    [Theory, ModeData]
    public void ReceiversSurviveSuspensionAndInterleavedInvocations(ExecutionMode mode)
        => Assert.Equal(InterleavedOutput, TestHarness.Run(InterleavedSource, mode));
}
