using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ForOfBodyLexicalCellTests
{
    public const string IssueSource = """
        for (const target of [{} as any, function() {} as any]) {
            let reads = 0;
            Object.defineProperty(target, "read", {
                get: function() { reads++; return 42; }
            });
            console.log(target.read, target.read, reads);
            const counter = { reads: 0 };
            Object.defineProperty(target, "control", {
                get: function() { counter.reads++; return 43; }
            });
            console.log(target.control, target.control, counter.reads);
        }
        """;

    public const string EscapedSiblingSource = """
        let count = 1000;
        const outside = () => count;
        const entries: any[] = [];
        for (const seed of [0, 10]) {
            let count = seed;
            const read = () => count;
            const bump = (step: number) => { count += step; return count; };
            count += 1;
            let shadow = 0;
            { let count = seed + 100; count++; shadow = count; }
            entries.push({ read, bump });
            console.log(read(), bump(2), read(), shadow);
        }
        console.log(entries[0].bump(4), entries[1].read(), entries[0].read());
        console.log(entries[1].bump(1), entries[0].read(), outside());
        """;

    public const string OrdinaryForAccessorSource = """
        for (let i = 0; i < 1; i++) {
            let reads = 0;
            const object: any = {};
            Object.defineProperty(object, Symbol.iterator, { get() { reads++; return undefined; } });
            object[Symbol.iterator];
            console.log(reads);
        }
        """;

    public const string OrdinaryForEscapedSiblingSource = """
        const entries: any[] = [];
        const headers: any[] = [];
        for (let index = 0; index < 2; index++) {
            let value = index * 10;
            const read = () => value;
            const bump = (step: number) => { value += step; return value; };
            entries.push({ read, bump });
            headers.push(() => index);
            value += 1;
        }
        console.log(entries[0].read(), entries[1].read());
        console.log(entries[0].bump(4), entries[1].read(), entries[0].read());
        console.log(entries[1].bump(2), entries[0].read());
        console.log(headers[0](), headers[1]());
        """;

    public const string CallableForwardShadowSource = """
        function collect() {
            let value = 1000;
            const outside = () => value;
            const entries: any[] = [];
            for (const seed of [1, 10]) {
                const forward = () => value;
                const constantRead = () => constant;
                let value = seed;
                const constant = seed + 100;
                const write = (next: number) => { value = next; };
                const inspect = () => {
                    const outer = value;
                    { const read = () => value; let value = 50; return [outer, read()]; }
                };
                {
                    let value = seed + 20;
                    const nestedRead = () => value;
                    const nestedBump = () => ++value;
                    entries.push({ forward, constantRead, write, inspect, nestedRead, nestedBump });
                }
                value += 1;
            }
            return { entries, outside };
        }
        const result = collect();
        const first = result.entries[0];
        const second = result.entries[1];
        console.log(first.forward(), second.forward(), first.constantRead(), second.constantRead());
        first.write(7);
        console.log(first.forward(), second.forward(), first.nestedBump(), first.nestedRead());
        console.log(second.nestedBump(), first.nestedRead(), second.forward(), result.outside());
        console.log(first.inspect().join(","), second.inspect().join(","));
        """;

    public const string InitializationAndPreservationSource = """
        for (const seed of [1, 2]) {
            const read = () => value;
            const write = (next: number) => { value = next; };
            try { read(); } catch (error: any) { console.log(error.name); }
            try { write(99); } catch (error: any) { console.log(error.name); }
            let value = seed;
            console.log(read());
            write(seed + 10);
            console.log(value);
        }
        const shared: any[] = [];
        function collectVar() {
            for (const seed of [1, 2]) {
                var value = seed;
                shared.push(() => ++value);
            }
        }
        collectVar();
        console.log(shared[0](), shared[1]());
        const headers: any[] = [];
        for (let index = 0; index < 2; index++) { headers.push(() => index); }
        console.log(headers[0](), headers[1]());
        """;

    public const string ForwardLetSource = """
        for (const seed of [1]) {
            const read = () => value;
            let value = seed;
            console.log(read());
        }
        """;

    public const string ForwardConstSource = """
        for (const seed of [1]) {
            const read = () => value;
            const value = seed;
            console.log(read());
        }
        """;

    public const string AnnotatedShadowSource = """
        let value: number = 100;
        {
            const read = (): string => value;
            let value: string = "inner";
            console.log(read());
        }
        console.log(value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "42 42 2\n43 43 2\n42 42 2\n43 43 2\n"];
        yield return ["escaped-siblings", EscapedSiblingSource, "1 3 3 101\n11 13 13 111\n7 13 7\n14 7 1000\n"];
        yield return ["arrow-owner", "const run = () => {\n" + EscapedSiblingSource + "\n};\nrun();",
            "1 3 3 101\n11 13 13 111\n7 13 7\n14 7 1000\n"];
        yield return ["ordinary-for-accessor", OrdinaryForAccessorSource, "1\n"];
        yield return ["ordinary-for-escaped-siblings", OrdinaryForEscapedSiblingSource,
            "1 11\n5 11 5\n13 5\n0 1\n"];
        yield return ["callable-forward-shadow", CallableForwardShadowSource,
            "2 11 101 110\n7 11 22 22\n31 22 11 1000\n7,50 11,50\n"];
        yield return ["initialization-preservation", InitializationAndPreservationSource,
            "ReferenceError\nReferenceError\n1\n11\nReferenceError\nReferenceError\n2\n12\n3 4\n0 1\n"];
        yield return ["forward-let", ForwardLetSource, "1\n"];
        yield return ["forward-const", ForwardConstSource, "1\n"];
        yield return ["annotated-shadow", AnnotatedShadowSource, "inner\n100\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ForOfBodyClosuresShareOnlyTheirOwnLexicalActivation(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ModuleBodyCellsRemainDistinctFromCapturedModuleBindings(ExecutionMode mode)
        => Assert.Equal((string)Cases().Single(row => (string)row[0] == "escaped-siblings")[2],
            TestHarness.RunModules(new Dictionary<string, string> { ["main.ts"] = EscapedSiblingSource }, "main.ts", mode));
}
