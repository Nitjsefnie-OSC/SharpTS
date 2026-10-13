using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BoundInstanceofTests
{
    public const string IssueSource = """
        class C { constructor(value: number) { console.log(value); } }
        const extractedBind: any = (() => 0).bind;
        const Bound: any = extractedBind.call(C, {}, 43);
        const value = new Bound();
        console.log(value instanceof C, value instanceof Bound);
        """;

    [Theory, ModeData]
    public void BoundClassInstanceMatchesBothTargetAndBinding(ExecutionMode mode)
    {
        Assert.Equal("43\ntrue true\n", TestHarness.Run(IssueSource, mode));
    }

    [Theory, ModeData]
    public void NestedBindingsDelegateToClassAndOrdinaryFunctionTargets(ExecutionMode mode)
    {
        const string source = """
            class Base {}
            class Derived extends Base {}
            const Bound: any = Derived.bind({});
            const Twice: any = Bound.bind({});
            const child = new Twice();
            console.log(child instanceof Base, child instanceof Derived,
                child instanceof Bound, child instanceof Twice);
            function C(this: any) {}
            const BoundFunction: any = C.bind({});
            const TwiceFunction: any = BoundFunction.bind({});
            const instance = new TwiceFunction();
            console.log(instance instanceof C, instance instanceof BoundFunction,
                instance instanceof TwiceFunction);
            console.log({} instanceof Twice, null instanceof Twice,
                1 instanceof TwiceFunction);
            """;
        Assert.Equal("true true true true\ntrue true true\nfalse false false\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void TargetPrototypeIsReadLiveAndBoundPrototypeIsIgnored(ExecutionMode mode)
    {
        const string source = """
            function C(this: any) {}
            const Bound: any = C.bind({});
            const Twice: any = Bound.bind({});
            const old: any = new C();
            const prototype: any = {};
            (C as any).prototype = prototype;
            const current: any = Object.create(prototype);
            Object.defineProperty(Bound, "prototype", {
                get() { throw "bound prototype must not be read"; }
            });
            Twice.prototype = {};
            console.log(old instanceof Bound, current instanceof Bound,
                current instanceof Twice);
            """;
        Assert.Equal("false true true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void RepeatedClassExpressionDefinitionsKeepSeparateBoundIdentities(ExecutionMode mode)
    {
        const string source = """
            function make() { return class {}; }
            const First: any = make(), Second: any = make();
            const BoundFirst: any = First.bind({});
            const BoundSecond: any = Second.bind({});
            const Twice: any = BoundFirst.bind({});
            const value = new Twice();
            console.log(value instanceof First, value instanceof Second,
                value instanceof BoundFirst, value instanceof BoundSecond,
                value instanceof Twice);
            """;
        Assert.Equal("true false true false true\n", TestHarness.Run(source, mode));
    }
}
