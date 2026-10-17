using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ExplicitSuperDefinitionArgumentsTests
{
    public const string IssueSource = """
        const C = class {
            constructor(...values: any[]);
            constructor(value?: any) {
                console.log(value, arguments.length, arguments[1]);
            }
        };
        const D = class extends C {
            constructor() { super(43, "tail"); }
        };
        new D();
        """;

    public const string DeclarationRestSource = """
        const Root = class { constructor(value: any) {} };
        class Base extends Root {
            constructor(value: any, ...rest: any[]) {
                super(value);
                console.log(value, rest.length, rest[0]);
            }
        }
        class D extends Base { constructor() { super(43, "tail"); } }
        new D();
        """;

    public const string ZeroParameterSource = """
        let calls = 0;
        function extra() { calls++; return "tail"; }
        const C = class {
            constructor(...values: any[]);
            constructor() { console.log(arguments.length, arguments[0]); }
        };
        const D = class extends C { constructor() { super(extra()); } };
        new D();
        console.log(calls);
        """;

    public const string SpreadAndMetadataSource = """
        const trace: string[] = [], marker = {};
        function observe(tag: string, value: any) { trace.push(tag); return value; }
        function* tail() {
            trace.push("spread");
            yield marker;
            trace.push("second");
            yield null;
            trace.push("done");
        }
        const Parent = class Named {
            constructor(...values: any[]);
            constructor(value: any = "default") {
                console.log(value, arguments.length, arguments[0] === undefined,
                    arguments[1] === marker, arguments[2] === null, arguments[3], new.target === Child);
            }
            owner() { return Named; }
        };
        const Child = class extends Parent {
            constructor(seed: number) {
                const before = arguments.length;
                super(observe("first", undefined), ...tail(), observe("last", "extra"));
                console.log(before, arguments.length, arguments[0]);
            }
        };
        const Construct: any = Child, value: any = new Construct(7);
        console.log(trace.join(","), value.owner() === Parent,
            value instanceof Parent, value instanceof Child);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["expression-child", IssueSource, "43 2 tail\n"];
        yield return ["declaration-child", IssueSource.Replace("const D = class extends C", "class D extends C"),
            "43 2 tail\n"];
        yield return ["declaration-rest", DeclarationRestSource, "43 1 tail\n"];
        yield return ["zero-parameters", ZeroParameterSource, "1 tail\n1\n"];
        yield return ["spread-and-metadata", SpreadAndMetadataSource,
            "default 4 true true true extra true\n1 1 7\nfirst,spread,second,done,last true true true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ExplicitSuperSeparatesGuestArgumentsFromConstructorMetadata(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ModuleExplicitSuperPreservesGuestArgumentsAndEvaluatedParentDefinition(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(
            new Dictionary<string, string> { ["main.ts"] = source }, "main.ts", mode));
    }
}
