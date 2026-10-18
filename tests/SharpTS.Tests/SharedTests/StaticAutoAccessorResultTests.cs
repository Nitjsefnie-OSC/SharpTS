using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticAutoAccessorResultTests
{
    public static Dictionary<string, string> Files(string source) => new()
    {
        ["main.ts"] = source,
        ["owner.ts"] = """
            export class Config {
                static accessor count: number = 5;
                static accessor ready: boolean = true;
                static accessor label: string = "imported";
            }
            export class Generic<T> {
                static accessor count: number = 7;
                static accessor ready: boolean = false;
            }
            """,
    };

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class C { static accessor count: number = 1; }
            C.count = 3;
            console.log(C.count);
            """, "3\n"];
        yield return ["primitive-and-nested-reads", """
            class Config {
                static accessor count: number = 1;
                static accessor ready: boolean = true;
                static accessor label: string = "one";
            }
            console.log(Config.count, Config.ready, Config.label);
            Config.count = 3;
            Config.ready = false;
            Config.label = "three";
            function show(value: any): void { console.log(value); }
            show([Config.count, Config.ready, Config.label].join(","));
            console.log(2 * (Config.count + 1), !Config.ready, Config.label + "!");
            """, "1 true one\n3,false,three\n8 true three!\n"];
        yield return ["function-static-this-and-inheritance", """
            class Base {
                static accessor count: number = 3;
                static accessor ready: boolean = true;
                static accessor label: string = "base";
                static read(): void { console.log(this.count, this.ready, this.label); }
            }
            class Derived extends Base {}
            function read(): void { console.log(Derived.count, Derived.ready, Derived.label); }
            read();
            Derived.read();
            """, "3 true base\n3 true base\n"];
        yield return ["import-and-generic-owner", """
            import { Config, Generic } from "./owner";
            console.log(Config.count, Config.ready, Config.label);
            console.log([Config.count, Config.ready].join(","));
            console.log(Generic.count, Generic.ready);
            """, "5 true imported\n5,true\n7 false\n"];
        yield return ["boxed-value-controls", """
            class Config {
                static accessor nullable: number | null = 1;
                static accessor big: bigint = 2n;
            }
            console.log(Config.nullable === 1, Config.big === 2n, typeof Config.big);
            Config.nullable = null;
            console.log(Config.nullable === null);
            """, "true true bigint\ntrue\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void StaticGetterResultsRetainTheirRuntimeRepresentation(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(Files(source), "main.ts", mode));
    }
}
