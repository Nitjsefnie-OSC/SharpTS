using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public class StaticSuperMethodTests
{
    public const string ReceiverSource = """
            class Base {
                static value() { return this; }
                value() { return "instance"; }
            }
            class Middle extends Base {}
            class Derived extends Middle {
                static read() { return super.value(); }
                static arrow() { return (() => super.value())(); }
            }
            class Child extends Derived {}
            const derived: any = Derived;
            const child: any = Child;
            const receiver: any = {};
            console.log(derived.read() === Derived);
            console.log(child.read() === Child);
            console.log(derived.read.call(receiver) === receiver);
            console.log(derived.arrow() === Derived);
            """;

    [Theory, ModeData]
    public void StaticSuperUsesTheLexicalParentAndCurrentReceiver(ExecutionMode mode)
    {
        Assert.Equal("true\ntrue\ntrue\ntrue\n", TestHarness.Run(ReceiverSource, mode));
    }

    [Theory, ModeData]
    public void StaticSuperPreservesTheNearestOverride(ExecutionMode mode)
    {
        const string source = """
            class Base { static value(x: number) { return x + 1; } }
            class Middle extends Base { static value(x: number) { return x + 2; } }
            class Derived extends Middle { static read() { return super.value(40); } }
            console.log(Derived.read());
            """;
        Assert.Equal("42\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void StaticSuperMethodValuesAreCanonicalAndObserveReplacement(ExecutionMode mode)
    {
        const string source = """
            class Base { static value() { return this; } }
            class Derived extends Base {
                static read() { return super.value; }
                static call() { return super.value(); }
            }
            const method: any = Derived.read();
            console.log(method === Base.value, method() === undefined);
            const base: any = Base;
            base.value = function() { return this; };
            console.log(Derived.read() === base.value, Derived.call() === Derived);
            """;
        Assert.Equal("true true\ntrue true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassExpressionArrowsRetainTheirStaticSuperHome(ExecutionMode mode)
    {
        const string source = """
            class Base { static value() { return this; } }
            const Derived: any = class extends Base {
                static make() { return () => super.value(); }
            };
            const receiver: any = {};
            console.log(Derived.make()() === Derived);
            console.log(Derived.make.call(receiver)() === receiver);
            """;
        Assert.Equal("true\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PrivateStaticMethodsRetainTheirSuperFacetForMethodValues(ExecutionMode mode)
    {
        const string source = """
            class Base { static value() { return this; } }
            class Derived extends Base {
                static #read() { const value: any = super.value; return value.call(this); }
                static read() { return this.#read(); }
            }
            console.log(Derived.read() === Derived);
            """;
        Assert.Equal("true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassExpressionInstanceInitializersKeepTheInstanceSuperFacet(ExecutionMode mode)
    {
        const string source = """
            class Base { value() { return "instance"; } static value() { return "static"; } }
            const Derived: any = class extends Base { result = super.value(); };
            console.log(new Derived().result);
            """;
        Assert.Equal("instance\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void RepeatedClassExpressionArrowsKeepEachEvaluatedParent(ExecutionMode mode)
    {
        const string source = """
            class First { static value() { return "first"; } }
            class Second { static value() { return "second"; } }
            function make(base: any) {
                return class extends base {
                    static read() { return (() => super.value())(); }
                    static async readAsync() { return await (async () => super.value())(); }
                };
            }
            const first: any = make(First), second: any = make(Second);
            console.log(first.read(), second.read(), first.read());
            async function run() {
                console.log(await first.readAsync(), await second.readAsync(), await first.readAsync());
            }
            run();
            """;
        Assert.Equal("first second first\nfirst second first\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void StaticSuperGetterUsesTheCurrentReceiverForCallableValues(ExecutionMode mode)
    {
        const string source = """
            class Base { static get value() { return () => this; } }
            class Derived extends Base { static read() { return super.value(); } }
            const derived: any = Derived, receiver: any = {};
            console.log(derived.read() === Derived);
            console.log(derived.read.call(receiver) === receiver);
            """;
        // Static accessor types are currently stored on the instance facet (#2035).
        // Exercise receiver-aware runtime lookup independently of that checker gap.
        Assert.Equal("true\ntrue\n", TestHarness.RunModules(
            new Dictionary<string, string> { ["main.ts"] = source }, "main.ts", mode, allowTypeErrors: true));
    }
}
