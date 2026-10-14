using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NestedClassMethodArrowCaptureTests
{
    public const string IssueSource = """
        function make() {
            class Inner { capture(value: any) { const local = value; return () => local; } }
            return new Inner();
        }
        const expected: any = {};
        const captured = make().capture(expected)();
        console.log(captured === expected, captured === null);
        """;

    private const string MutableMethod = """
        capture(value: any) {
            let local: any = value;
            let count: number = 1;
            let flag: boolean = false;
            const read = () => [local, count, flag];
            const write = (next: any) => { local = next; count += 1; flag = !flag; };
            count = 2;
            return { read, write };
        }
        """;

    private const string MutableCalls = """
        const instance = make(), expected: any = {}, replacement: any = {}, other: any = {};
        const first: any = instance.capture(expected), second: any = instance.capture(other);
        let values: any = first.read();
        console.log(values[0] === expected, values[1], values[2]);
        first.write(replacement);
        values = first.read();
        console.log(values[0] === replacement, values[1], values[2]);
        values = second.read();
        console.log(values[0] === other, values[1], values[2]);
        """;

    public const string ParameterSelfSource = """
        function make() {
            class C {
                capture(C: any) { return () => C; }
                self() { return C; }
            }
            return new C();
        }
        const expected: any = {};
        console.log(make().capture(expected)() === expected);
        """;

    public const string ConstructorSource = """
        const E = class {
            read: any;
            constructor(value: number) {
                const stable: number = 44;
                this.read = () => [value, typeof value, stable];
            }
        };
        function constructor(value: number) {
            const stable: number = 45;
            return () => [value, typeof value, stable];
        }
        const bind: any = (() => 0).bind;
        const Bound: any = bind.call(E, {}, null);
        const captured: any = new Bound().read();
        console.log(captured[0], captured[1], captured[2]);
        const ordinary: any = constructor(8)();
        console.log(ordinary[0], ordinary[1], ordinary[2]);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true false\n"];
        yield return ["parameter-self", ParameterSelfSource, "true\n"];
        yield return ["constructor", ConstructorSource, "null object 44\n8 number 45\n"];
        yield return ["mutable-declaration", $$"""
            function make() { class Inner { {{MutableMethod}} } return new Inner(); }
            {{MutableCalls}}
            """, "true 2 false\ntrue 3 true\ntrue 2 false\n"];
        yield return ["mutable-expression", $$"""
            function make() { const C = class Named { {{MutableMethod}} }; return new C(); }
            {{MutableCalls}}
            """, "true 2 false\ntrue 3 true\ntrue 2 false\n"];
        yield return ["mutable-anonymous", $$"""
            function make() { const C = class { {{MutableMethod}} }; return new C(); }
            {{MutableCalls}}
            """, "true 2 false\ntrue 3 true\ntrue 2 false\n"];
        yield return ["mutable-nested-method", $$"""
            class Outer { make() { class Inner { {{MutableMethod}} } return new Inner(); } }
            const make = (): any => new Outer().make();
            {{MutableCalls}}
            """, "true 2 false\ntrue 3 true\ntrue 2 false\n"];
        yield return ["mutable-arrow-factory", $$"""
            const make = (): any => { class Inner { {{MutableMethod}} } return new Inner(); };
            {{MutableCalls}}
            """, "true 2 false\ntrue 3 true\ntrue 2 false\n"];
        yield return ["top-level-control", """
            class C { capture(value: any) { const local = value; return () => local; } }
            const E = class { capture(value: any) { const local = value; return () => local; } };
            const expected: any = {};
            console.log(new C().capture(expected)() === expected, new E().capture(expected)() === expected);
            """, "true true\n"];
        yield return ["static", """
            function make() {
                class C { static capture() {
                    let value = 1;
                    const read = () => value;
                    const increment = () => ++value;
                    value = 2;
                    return { read, increment };
                } }
                return C;
            }
            const C = make(), closures: any = C.capture();
            console.log(closures.read(), closures.increment(), closures.read());
            """, "2 3 3\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void MethodClosuresRetainTheirInvocationEnvironment(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    public static Dictionary<string, string> ModuleFiles() => new()
    {
        ["factory.ts"] = $$"""
            export function make() { class Inner { {{MutableMethod}} } return new Inner(); }
            """,
        ["main.ts"] = "import { make } from './factory';\n" + MutableCalls
    };

    [Theory, ModeData]
    public void ModuleFactoryMethodClosuresRetainSharedStorage(ExecutionMode mode)
        => Assert.Equal("true 2 false\ntrue 3 true\ntrue 2 false\n",
            TestHarness.RunModules(ModuleFiles(), "main.ts", mode));

    [Fact]
    public void ModuleFactoryMethodClosuresVerify()
        => Assert.Empty(TestHarness.CompileModulesAndVerifyOnly(ModuleFiles(), "main.ts"));
}
