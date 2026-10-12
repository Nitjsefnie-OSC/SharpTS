using SharpTS.Tests.Infrastructure;
using SharpTS.Parsing;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BoundClassArgumentTests
{
    internal const string OptionalDefaultRestSource = """
        const extractedBind: any = (() => 0).bind;
        class Defaulted { constructor(value: number = 44) { console.log(value); } }
        class Optional { constructor(value?: number) { console.log(value === undefined); } }
        class Rest { constructor(...values: number[]) { console.log(values.join(",")); } }
        const BoundDefault: any = extractedBind.call(Defaulted, {});
        const BoundOptional: any = extractedBind.call(Optional, {});
        const BoundRest: any = extractedBind.call(Rest, {}, 1);
        new BoundDefault();
        new BoundOptional();
        new BoundRest(2, 3);
        """;

    [Theory, ModeData]
    public void ExtractedBindShapesOptionalDefaultAndRestArguments(ExecutionMode mode)
    {
        Assert.Equal("44\ntrue\n1,2,3\n", TestHarness.Run(OptionalDefaultRestSource, mode));
    }

    [Theory, ModeData]
    public void RepeatedBindingKeepsDefaultsAndRestInSourceOrder(ExecutionMode mode)
    {
        const string source = """
            class C {
                constructor(first: number, second: any = 44, ...tail: number[]) {
                    console.log(first, second, tail.join(","));
                }
            }
            const First: any = C.bind({}, 1);
            const Missing: any = First.bind({}, undefined);
            new Missing(2, 3);
            const Null: any = First.bind({}, null);
            new Null(4, 5);
            """;
        Assert.Equal("1 44 2,3\n1 null 4,5\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void OrdinaryArrayParameterIsOneArgument(ExecutionMode mode)
    {
        const string source = """
            const expected: any = [1, 2];
            class C { constructor(values: number[]) { console.log(values === expected, values.length, values[0]); } }
            const extractedBind: any = (() => 0).bind;
            const Bound: any = extractedBind.call(C, {}, expected);
            new Bound();
            """;
        Assert.Equal("true 2 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ConstructorArgumentsIncludeBoundPrefixAndSurplusValues(ExecutionMode mode)
    {
        const string source = """
            class C {
                constructor(first: number) {
                    console.log(arguments.length, arguments[0], arguments[1], arguments[2]);
                }
            }
            const Bound: any = C.bind({}, 1);
            new Bound(2, 3);
            class Exact {
                constructor(first: number, second: number, third: number) {
                    console.log(arguments.length, arguments[0], arguments[1], arguments[2]);
                }
            }
            const BoundExact: any = Exact.bind({}, 4);
            new BoundExact(5, 6);
            """;
        Assert.Equal("3 1 2 3\n3 4 5 6\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void GuestThrownObjectsKeepIdentity(ExecutionMode mode)
    {
        const string source = """
            const token: any = {};
            class C { constructor(value: number = 44) { throw token; } }
            const Bound: any = C.bind({});
            try { new Bound(); } catch (error) { console.log(error === token); }
            """;
        Assert.Equal("true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ImplicitSubclassesInheritDefaultAndRestArgumentShape(ExecutionMode mode)
    {
        const string source = """
            class Base { constructor(value: number = 44) { console.log(value); } }
            class Middle extends Base {}
            class Derived extends Middle {}
            class RestBase {
                constructor(first: number = 45, ...tail: number[]) {
                    console.log(first, tail.join(","), arguments.length);
                }
            }
            class RestDerived extends RestBase {}
            const BoundDefault: any = Derived.bind({});
            const BoundRest: any = RestDerived.bind({}, undefined, 1);
            new BoundDefault();
            new BoundRest(2, 3);
            """;
        Assert.Equal("44\n45 1,2,3 4\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ConstructorFactoryIsAbsentFromGuestOwnProperties(ExecutionMode mode)
    {
        const string source = """
            class C { constructor(value: number = 44) {} }
            const Bound: any = C.bind({});
            new Bound();
            console.log(Object.getOwnPropertyNames(C).includes("$ConstructFromArguments"));
            console.log(Object.hasOwn(C, "$ConstructFromArguments"));
            """;
        Assert.Equal("false\nfalse\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClosedGenericSuperclassKeepsConstructorShape(ExecutionMode mode)
    {
        const string source = """
            class Base<T> { constructor(value: T, suffix: number = 44) { console.log(value, suffix); } }
            class Derived extends Base<string> {}
            const Bound: any = Derived.bind({}, "first");
            new Bound();
            """;
        Assert.Equal("first 44\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void CompiledRequiredPrimitiveSlotsPreserveSuppliedNullConversion()
    {
        // The existing CLR constructor ABI converts supplied null to 0/false.
        // Node and the interpreter retain null (#2040); this preserves compiled behavior.
        const string source = """
            class C { constructor(value: number, flag: boolean) { console.log(value, flag); } }
            const extractedBind: any = (() => 0).bind;
            const Bound: any = extractedBind.call(C, {}, null, null);
            new Bound();
            """;
        Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal("0 false\n", TestHarness.Run(source, ExecutionMode.Compiled));
    }

    internal const string BoxedIntegerSource = """
        @DotNetType("System.Convert")
        declare class Convert { static toInt32(value: number): number; }
        class C { constructor(value: number) { console.log(value); } }
        const extractedBind: any = (() => 0).bind;
        const value: any = Convert.toInt32(7);
        const Bound: any = extractedBind.call(C, {}, value);
        new Bound();
        """;

    [Fact]
    public void CompiledRequiredNumberSlotPreservesClrPrimitiveWidening()
    {
        Assert.Equal("7\n", TestHarness.RunCompiled(BoxedIntegerSource, DecoratorMode.Legacy));
    }
}
