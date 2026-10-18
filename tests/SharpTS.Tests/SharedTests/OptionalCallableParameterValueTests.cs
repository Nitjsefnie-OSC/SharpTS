using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OptionalCallableParameterValueTests
{
    public const string Exact = """
        const read = (first: number, second?: string) =>
            first + "|" + (second === undefined ? "missing" : second);
        console.log(read(12));
        console.log(read(12, undefined));
        """;

    public const string RawDirectAndValueCalls = """
        function classify(value: any): string {
            return typeof value + ":" + (value === undefined) + ":" + (value === null) + ":" + value;
        }
        const arrow = (value?: string): string => classify(value);
        const expression = function(value?: string): string { return classify(value); };
        console.log(arrow(), arrow(undefined), arrow(null as any), arrow(""), arrow("ok"));
        console.log(expression(), expression(undefined), expression(null as any), expression(""), expression("ok"));
        const arrowValue: any = arrow, expressionValue: any = expression;
        console.log(arrowValue(), arrowValue(undefined), arrowValue(null), arrowValue(""), arrowValue("ok"));
        console.log(expressionValue.call(null), expressionValue.call(null, undefined), expressionValue.call(null, null),
            expressionValue.apply(null, [""]), expressionValue.apply(null, ["ok"]));
        """;

    public const string PrivateField = """
        class C {
            static #read: (first: number, second?: string) => string = (first: number, second?: string): string =>
                first + "|" + (second === undefined ? "missing" : second);
            static run(): void {
                console.log(this.#read(12), this.#read(12, undefined as any), this.#read(12, null as any), this.#read(12, "ok"));
            }
        }
        C.run();
        """;

    public const string DefaultControls = """
        function classify(value: any): string {
            return typeof value + ":" + (value === undefined) + ":" + (value === null) + ":" + value;
        }
        const arrow = (value: string = "fallback"): string => classify(value);
        const expression = function(value: string = "fallback"): string { return classify(value); };
        console.log(arrow(), arrow(undefined), arrow(null as any), arrow(""), arrow("ok"));
        const dynamic: any = expression;
        console.log(dynamic(), dynamic(undefined), dynamic(null), dynamic(""), dynamic("ok"));
        """;

    public const string BclDelegateControl = """
        declare function DotNetType(name: string): any;
        @DotNetType("System.Array")
        declare class NativeArray {
            static Exists<T>(values: T[], predicate: (value: T) => boolean): boolean;
        }
        const optional = (value?: string): boolean => value === "hello";
        const required = (value: string): boolean => value === "hello";
        if (typeof NativeArray === "undefined") {
            console.log(["hello"].some(optional), ["hello"].some(required), ["bye"].some(optional));
        } else {
            console.log(NativeArray.Exists(["hello"], optional), NativeArray.Exists(["hello"], required),
                NativeArray.Exists(["bye"], optional));
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        const string rawLine = "undefined:true:false:undefined undefined:true:false:undefined object:false:true:null string:false:false: string:false:false:ok\n";
        const string defaultLine = "string:false:false:fallback string:false:false:fallback object:false:true:null string:false:false: string:false:false:ok\n";
        yield return ["exact-optional-string", Exact, "12|missing\n12|missing\n"];
        yield return ["raw-direct-and-value-calls", RawDirectAndValueCalls, rawLine + rawLine + rawLine + rawLine];
        yield return ["private-callable-field", PrivateField, "12|missing 12|missing 12|null 12|ok\n"];
        yield return ["default-parameter-controls", DefaultControls, defaultLine + defaultLine];
        yield return ["bcl-delegate-control", BclDelegateControl, "true true false\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OptionalCallableParametersPreserveRawValues(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        var decorators = name == "bcl-delegate-control" ? DecoratorMode.Legacy : DecoratorMode.None;
        Assert.Equal(expected, TestHarness.Run(source, mode, decorators));
    }
}
