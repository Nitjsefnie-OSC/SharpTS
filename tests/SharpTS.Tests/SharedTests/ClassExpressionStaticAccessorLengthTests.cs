using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionStaticAccessorLengthTests
{
    public const string IssueSource = """
        const C = class { static get value() { return 1; } static set value(next: number) {} };
        const descriptor = Object.getOwnPropertyDescriptor(C, "value")!;
        console.log(descriptor.get!.length, descriptor.set!.length);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "0 1\n"];
        yield return ["captured-definition-and-calls", """
            function make(value: number) {
                return class Named {
                    static get result() { return value; }
                    static set result(next: number) { value = next; }
                };
            }
            const First = make(41), Second = make(42);
            const descriptor = Object.getOwnPropertyDescriptor(First, "result")!;
            console.log(descriptor.get!.length, descriptor.set!.length, descriptor.get!.call(First));
            descriptor.set!.call(First, 43);
            console.log(First.result, Second.result);
            const length = Object.getOwnPropertyDescriptor(descriptor.get!, "length")!;
            console.log(length.value, length.writable, length.enumerable, length.configurable);
            """, "0 1 41\n43 42\n0 false false true\n"];
        yield return ["declaration-and-instance-controls", """
            class Declared { static get value() { return 1; } static set value(next: number) {} }
            const Expression = class { get value() { return 2; } set value(next: number) {} };
            const declared = Object.getOwnPropertyDescriptor(Declared, "value")!;
            const instance = Object.getOwnPropertyDescriptor(Expression.prototype, "value")!;
            console.log(declared.get!.length, declared.set!.length, instance.get!.length, instance.set!.length);
            """, "0 1 0 1\n"];
        yield return ["symbol-accessors", """
            const key = Symbol("value"), C = class { static get [key]() { return 1; } static set [key](next: number) {} };
            const descriptor = Object.getOwnPropertyDescriptor(C, key)!;
            console.log(descriptor.get!.length, descriptor.set!.length, descriptor.get!.call(C));
            """, "0 1 1\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void StaticAccessorLengthCountsOnlyGuestParameters(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
