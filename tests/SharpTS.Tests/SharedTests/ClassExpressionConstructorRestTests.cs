using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionConstructorRestTests
{
    public const string IssueSource = """
        const C = class {
            constructor(...values: any[]) { console.log(values.length, values[0], values[1]); }
        };
        new C(43, 44);
        """;

    public const string ShapesSource = """
        const C = class {
            values: any[];
            raw: any[];
            constructor(...values: any[]) { this.values = values; this.raw = Array.from(arguments); }
        };
        const empty = new C();
        console.log(Array.isArray(empty.values), empty.values.length,
            empty.values[0] === undefined, empty.raw.length);
        const single = new C(43);
        console.log(single.values.length, single.values[0], single.raw.length);
        const object = {};
        const nested = [7];
        const first = new C(undefined, null, object, nested);
        const second = new C(undefined, null, object, nested);
        console.log(Array.isArray(first.values), first.values.length, first.values[0] === undefined,
            first.values[1] === null, first.values[2] === object, first.values[3] === nested,
            first.raw.length, first.raw[0] === undefined, first.raw[2] === object);
        first.values.push("changed");
        console.log(first.values !== second.values, first.values.length,
            second.values.length, first.raw.length, nested.length);
        """;

    public const string ShapesExpected = "true 0 true 0\n1 43 1\n"
        + "true 4 true true true true 4 true true\ntrue 5 4 4 1\n";

    public const string PrefixSource = """
        const C = class {
            constructor(prefix: any = "default", ...values: any[]) {
                console.log(prefix, Array.isArray(values), values.length, values[0],
                    arguments.length, arguments[0], arguments[1]);
            }
        };
        new C();
        new C(undefined, null);
        new C(null, undefined);
        new C(43, 44, 45);
        """;

    public const string PrefixExpected = "default true 0 undefined 0 undefined undefined\n"
        + "default true 1 null 2 undefined null\nnull true 1 undefined 2 null undefined\n"
        + "43 true 2 44 3 43 44\n";

    public const string SpreadSource = """
        const trace: string[] = [];
        function value(tag: string, item: number) { trace.push(tag); return item; }
        function* spread() {
            trace.push("spread");
            yield value("yield0", 2);
            yield value("yield1", 3);
            trace.push("done");
        }
        const C = class {
            constructor(prefix: number, ...values: number[]) {
                console.log(prefix, values.join(","), arguments.length);
            }
        };
        new C(value("first", 1), ...spread(), value("last", 4));
        console.log(trace.join(","));
        """;

    public const string SpreadExpected = "1 2,3,4 4\nfirst,spread,yield0,yield1,done,last\n";

    public const string ConstructionPathsSource = """
        const C = class {
            values: any[];
            constructor(...values: any[]) { this.values = values; }
        };
        const Alias: any = C;
        const receiver: any = {};
        const Bound: any = Alias.bind(receiver, "bound");
        console.log(new Alias(43, 44).values.join(","), new Bound("tail").values.join(","));
        console.log(receiver.values === undefined);
        const Child = class extends C {};
        const child = new Child(45, 46);
        console.log(child.values.join(","), child instanceof C, child instanceof Child);
        new (class { constructor(...values: number[]) { console.log(values.join(",")); } })(47, 48);
        """;

    public const string ConstructionPathsExpected = "43,44 bound,tail\ntrue\n45,46 true true\n47,48\n";

    public const string GenericSource = """
        const C = class<T> {
            constructor(...values: T[]) { console.log(values.length, values[0], values[1]); }
        };
        new C<number>(43, 44);
        new C("x", "y");
        """;

    public const string DeclarationParentSource = """
        class Base {
            constructor(prefix: any = "default", ...values: any[]) {
                console.log(prefix, Array.isArray(values), values.length, values[0],
                    arguments.length, arguments[0], arguments[1]);
            }
        }
        const Child = class extends Base {};
        new Child();
        new Child(null);
        new Child("prefix", null);
        new Child("prefix", 43, 44);
        """;

    public const string DeclarationParentExpected = "default true 0 undefined 0 undefined undefined\n"
        + "null true 0 undefined 1 null undefined\nprefix true 1 null 2 prefix null\n"
        + "prefix true 2 43 3 prefix 43\n";

    public const string AlternateNewTargetSource = """
        const C = class {
            prefix: any;
            values: any[];
            raw: any[];
            target: any;
            constructor(prefix: any, ...values: any[]) {
                this.prefix = prefix;
                this.values = values;
                this.raw = Array.from(arguments);
                this.target = new.target;
            }
        };
        function Alternate() {}
        const object = {}, nested = [7];
        const argumentsList: any[] = ["prefix", undefined, null, object, nested];
        const first: any = Reflect.construct(C, argumentsList, Alternate);
        const second: any = Reflect.construct(C, argumentsList, Alternate);
        console.log(Object.getPrototypeOf(first) === Alternate.prototype,
            first instanceof Alternate, first instanceof C, first.target === Alternate);
        console.log(first.prefix, Array.isArray(first.values), first.values.length,
            first.values[0] === undefined, first.values[1] === null,
            first.values[2] === object, first.values[3] === nested);
        console.log(first.raw.length, first.raw[0], first.raw[1] === undefined,
            first.raw[3] === object, first.raw[4] === nested);
        first.values[0] = "changed";
        console.log(first.values !== second.values, first.values !== argumentsList,
            first.values !== first.raw, second.values[0] === undefined,
            first.raw[1] === undefined, argumentsList[1] === undefined);
        """;

    public const string AlternateNewTargetExpected = "true true false true\n"
        + "prefix true 4 true true true true\n5 prefix true true true\n"
        + "true true true true true true\n";

    public const string ModuleClassSource = """
        export const C = class {
            values: any[];
            constructor(...values: any[]) { this.values = values; console.log(arguments.length); }
        };
        """;

    public const string ModuleMainSource = """
        import { C } from "./values";
        const direct = new C(43, 44);
        const Alias: any = C;
        const empty = new Alias();
        console.log(direct.values.join(","), Array.isArray(empty.values), empty.values.length,
            empty.values[0] === undefined);
        """;

    public const string ModuleExpected = "2\n0\n43,44 true 0 true\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "2 43 44\n"];
        yield return ["shapes", ShapesSource, ShapesExpected];
        yield return ["prefix", PrefixSource, PrefixExpected];
        yield return ["spread", SpreadSource, SpreadExpected];
        yield return ["construction-paths", ConstructionPathsSource, ConstructionPathsExpected];
        yield return ["generic", GenericSource, "2 43 44\n2 x y\n"];
        yield return ["declaration-parent", DeclarationParentSource, DeclarationParentExpected];
        yield return ["alternate-new-target", AlternateNewTargetSource, AlternateNewTargetExpected];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ExpressionConstructorsPackFreshRestArrays(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ImportedExpressionConstructorsPreserveRestAndRawArguments(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["values.ts"] = ModuleClassSource,
            ["main.ts"] = ModuleMainSource
        };
        Assert.Equal(ModuleExpected, TestHarness.RunModules(files, "main.ts", mode));
    }
}
