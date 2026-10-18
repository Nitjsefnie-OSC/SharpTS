using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class OrdinaryBlockCapturedOwnerShadowTests
{
    public const string Exact = """
        function outer(value: number): void {
            const read = () => value;
            { let value: number = 7; console.log("inner", value); }
            console.log("outer", value, read());
        }
        outer(5);
        """;

    public const string OriginalBindingIsolation = """
        function raw(): number { return null as any; }
        function safe(): number { return 7; }
        function outer(value: number): number {
            const read = () => value;
            { let value: number = safe(); console.log("clean-block", value); }
            function child(value: number): number {
                let local: number = safe();
                return value + local;
            }
            { let value: number = raw(); console.log("raw-block", value); }
            let captured: number = 1;
            const mutate = () => { captured = raw(); };
            const readCaptured = () => captured;
            mutate();
            console.log("capture", readCaptured(), captured);
            console.log("outer", value, read(), child(2));
            return value;
        }
        function conversions(): void {
            const plus: number = +raw();
            const arithmetic: number = raw() + 2;
            const compared: boolean = raw() === 0;
            console.log("coerce", plus, arithmetic, compared);
        }
        console.log("clean-return", outer(5));
        conversions();
        """;

    public const string CapturedInnerIdentity = """
        function make(value: number) {
            const readOuter = () => value;
            let readInner: () => number;
            let changeInner: (next: number) => number;
            {
                let value: number = 10;
                readInner = () => value;
                changeInner = (next: number) => { value = next; return value; };
            }
            const changeOuter = (next: number) => { value = next; return value; };
            return { readOuter, readInner, changeOuter, changeInner };
        }
        const first = make(1), second = make(2);
        console.log("initial", first.readOuter(), first.readInner(), second.readOuter(), second.readInner());
        console.log("inner", first.changeInner(11), first.readInner(), second.readInner(), first.readOuter());
        console.log("outer", first.changeOuter(3), first.readOuter(), first.readInner(), second.readOuter());
        console.log("second", second.changeInner(22), second.changeOuter(4), second.readInner(), second.readOuter(), first.readInner(), first.readOuter());
        """;

    public const string ModuleMain = """
        import './left';
        import './right';
        """;

    public const string ModuleLeft = """
        export {};
        const base = 1;
        """;

    public const string ModuleRight = """
        export {};
        {
            const base = 5;
            const read = async () => base + 1;
            read().then(v => console.log(v), e => console.log(String(e)));
        }
        """;

    public const string NamespacePrivateRoot = """
        namespace Hidden { var base = 1; }
        {
            const base = 5;
            const read = async () => base + 1;
            read().then(v => console.log(v), e => console.log(String(e)));
        }
        """;

    public static Dictionary<string, string> ModuleFiles() => new()
    {
        ["main.ts"] = ModuleMain,
        ["left.ts"] = ModuleLeft,
        ["right.ts"] = ModuleRight
    };

    public static IEnumerable<object[]> Cases()
    {
        yield return ["literal-shadow", Exact, "inner 7\nouter 5 5\n"];
        yield return ["original-binding-isolation", OriginalBindingIsolation, "clean-block 7\nraw-block null\ncapture null null\nouter 5 5 9\nclean-return 5\ncoerce 0 2 false\n"];
        yield return ["captured-inner-identity", CapturedInnerIdentity, "initial 1 10 2 10\ninner 11 11 10 1\nouter 3 3 11 2\nsecond 22 4 22 4 11 3\n"];
        yield return ["namespace-private-root", NamespacePrivateRoot, "6\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OrdinaryBlocksPreserveCapturedOwnerBindings(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void CapturedBlockBindingsKeepTheirModuleOwner(ExecutionMode mode)
    {
        Assert.Equal("6\n", TestHarness.RunModules(ModuleFiles(), "main.ts", mode));
    }
}
