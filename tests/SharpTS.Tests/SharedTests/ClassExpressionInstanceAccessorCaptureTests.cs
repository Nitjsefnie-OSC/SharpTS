using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionInstanceAccessorCaptureTests
{
    public const string IssueSource = """
        function make(value: number) {
            return class { get result() { return value; } static get other() { return value; } };
        }
        const C = make(41), instance: any = new C();
        const getter: any = Object.getOwnPropertyDescriptor(C.prototype, "result")!.get;
        console.log(instance.result, getter.call({}), C.other);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "41 41 41\n"];
        yield return ["named-and-repeated", """
            function make(value: number) {
                return class Named { get result() { return value; } get self() { return Named; } };
            }
            const First = make(41), Second = make(42), first = new First(), second = new Second();
            const getter: any = Object.getOwnPropertyDescriptor(First.prototype, "result")!.get;
            console.log(first.result, second.result, getter.call(second), first.self === First, second.self === Second);
            """, "41 42 41 true true\n"];
        yield return ["mutable-getter-setter-receivers", """
            function make(value: number) {
                return class Named {
                    get result() { return value + (this as any).offset; }
                    set result(next: number) { value = next - (this as any).offset; }
                    static get read() { return value; }
                };
            }
            const First: any = make(10), Second: any = make(20), first: any = new First(), second: any = new Second();
            first.offset = 1; second.offset = 2;
            const descriptor = Object.getOwnPropertyDescriptor(First.prototype, "result")!;
            console.log(first.result, second.result, descriptor.get!.call({ offset: 3 }));
            descriptor.set!.call({ offset: 4 }, 30);
            console.log(first.result, First.read, second.result, Second.read);
            """, "11 22 13\n27 26 22 20\n"];
        yield return ["nested-immutable-captures", """
            function outer(prefix: string) {
                return function make(value: number) { return class { get result() { return prefix + value; } }; };
            }
            const make = outer("v"), First = make(41), Second = make(42);
            console.log(new First().result, new Second().result);
            """, "v41 v42\n"];
        yield return ["symbol-accessor", """
            const key = Symbol("result");
            function make(value: number) { return class { get [key]() { return value; } }; }
            const First = make(41), Second = make(42), first: any = new First(), second: any = new Second();
            const getter: any = Object.getOwnPropertyDescriptor(First.prototype, key)!.get;
            console.log(first[key], second[key], getter.call({}));
            """, "41 42 41\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        // Compiled transitive factory-to-class captures are tracked in #2253.
        ((string)row[0] == "nested-immutable-captures"
            ? new[] { ExecutionMode.Interpreted }
            : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InstanceAccessorsRetainFactoryCaptures(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
