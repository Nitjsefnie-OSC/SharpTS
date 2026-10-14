using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassDefinitionArrowCaptureTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["mixed", """
            function make(value: number, outer: any) {
                const C = class {
                    capture(item: any) {
                        let count = 1, local: any = item;
                        const read = () => [value, count, outer, local];
                        const write = (next: number, item: any) => {
                            value = next; count++; outer = item; local = item;
                        };
                        count = 2;
                        return { read, write };
                    }
                };
                value += 10;
                return C;
            }
            const expected: any = {}, other: any = {}, replacement: any = {};
            const First = make(7, expected), Second = make(9, other);
            const instance = new First();
            const first: any = instance.capture(expected), sibling: any = instance.capture(other);
            const separate: any = new Second().capture(other);
            let values: any = first.read();
            console.log(values[0], values[1], values[2] === expected, values[3] === expected);
            first.write(21, replacement);
            values = first.read();
            console.log(values[0], values[1], values[2] === replacement, values[3] === replacement);
            values = sibling.read();
            console.log(values[0], values[1], values[2] === replacement, values[3] === other);
            values = separate.read();
            console.log(values[0], values[1], values[2] === other, values[3] === other);
            """, "17 2 true true\n21 3 true true\n21 2 true true\n19 2 true true\n"];
        yield return ["shadows", """
            function make(value: any) {
                return class Named {
                    factory() { return () => value; }
                    parameter(value: any) { return () => value; }
                    local() {
                        let value: any = {};
                        const read = () => value;
                        const write = (next: any) => { value = next; };
                        return { read, write };
                    }
                    nested() { return (value: any) => () => value; }
                };
            }
            const expected: any = {}, other: any = {};
            const First = make(expected), Second = make(other);
            const instance = new First(), local: any = instance.local();
            local.write(other);
            console.log(instance.factory()() === expected, new Second().factory()() === other);
            console.log(instance.parameter(other)() === other, local.read() === other);
            console.log(instance.nested()(other)() === other, instance.factory()() === expected);
            """, "true true\ntrue true\ntrue true\n"];
        yield return ["async-mixed", """
            function make(value: number) {
                return class {
                    async capture() {
                        let count = 1;
                        await Promise.resolve(0);
                        const read = () => [value, count];
                        const write = (next: number) => { value = next; count++; };
                        return { read, write };
                    }
                };
            }
            async function run() {
                const First = make(7), Second = make(9);
                const first: any = await new First().capture(), sibling: any = await new First().capture();
                const separate: any = await new Second().capture();
                console.log(first.read().join(","), sibling.read().join(","), separate.read().join(","));
                first.write(21);
                console.log(first.read().join(","), sibling.read().join(","), separate.read().join(","));
            }
            run();
            """, "7,1 7,1 9,1\n21,2 21,1 9,1\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void MethodClosuresKeepDistinctLiveClassAndInvocationBindings(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled) Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
