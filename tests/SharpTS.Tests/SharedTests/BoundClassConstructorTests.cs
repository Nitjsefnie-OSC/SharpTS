using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BoundClassConstructorTests
{
    [Theory, ModeData]
    public void BoundClassConstructsFreshInstancesAndIgnoresBoundReceiver(ExecutionMode mode)
    {
        const string source = """
            class C { value: number; constructor(value: number) { this.value = value; } }
            const receiver: any = {};
            const Bound: any = C.bind(receiver, 43);
            const first = new Bound(), second = new Bound();
            console.log(first.value, second.value, first !== second);
            console.log(first instanceof C, Object.getPrototypeOf(first) === C.prototype);
            console.log(receiver.value === undefined);
            try { Bound(); } catch (error) { console.log(error instanceof TypeError); }
            """;
        if (mode == ExecutionMode.Compiled)
            Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal("43 43 true\ntrue true\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void RepeatedBindingsPrependArgumentsInOrder(ExecutionMode mode)
    {
        const string source = """
            class C {
                constructor(first: number, second: number, third: number) {
                    console.log(first, second, third);
                }
            }
            const first: any = C.bind({}, 1);
            const second: any = first.bind({}, 2);
            new second(3);
            const third: any = second.bind({}, 4);
            new third();
            """;
        Assert.Equal("1 2 3\n1 2 4\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void BoundClassExpressionsAndSubclassesRemainConstructible(ExecutionMode mode)
    {
        const string source = """
            const C = class { value: number; constructor(value: number) { this.value = value; } };
            class Base { value: number; constructor(value: number) { this.value = value; } }
            class Derived extends Base { constructor(value: number) { super(value); } }
            const Bound: any = C.bind({}, 42);
            const BoundDerived: any = Derived.bind({}, 43);
            const value = new Bound(), child = new BoundDerived();
            console.log(value.value, value instanceof C);
            console.log(child.value, child instanceof Derived, child instanceof Base);
            """;
        Assert.Equal("42 true\n43 true true\n", TestHarness.Run(source, mode));
    }
}
