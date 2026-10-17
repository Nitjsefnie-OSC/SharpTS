using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GenericAccessorKeyEvaluationTests
{
    public const string IssueSource = """
        const symbol = Symbol("key");
        let evaluations = 0;
        function key() { evaluations++; return symbol; }
        class C<T> { get [key()]() { return 1; } }
        new C<number>(); new C<string>();
        console.log(evaluations);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "1\n"];
        yield return ["paired-accessors-and-definition-timing", """
            const symbol = Symbol("key"), events: string[] = [];
            function key(label: string) { events.push(label); return symbol; }
            class C<T> {
                get [key("get")]() { return 1; }
                set [key("set")](value: number) {}
            }
            console.log(events.join(","));
            const first: any = new C<number>(), second: any = new C<string>();
            const descriptor = Object.getOwnPropertyDescriptor(C.prototype, symbol)!;
            console.log(events.join(","), first[symbol], second[symbol], typeof descriptor.get, typeof descriptor.set);
            """, "get,set\nget,set 1 1 function function\n"];
        yield return ["string-key-and-separate-definitions", """
            let evaluations = 0;
            function key() { evaluations++; return "result"; }
            class First<T> { get [key()]() { return 1; } }
            class Second<T> { get [key()]() { return 2; } }
            console.log(evaluations);
            const first: any = new First<number>(), second: any = new Second<string>();
            new First<boolean>(); new Second<number>();
            console.log(evaluations, first.result, second.result);
            """, "2\n2 1 2\n"];
        yield return ["static-and-instance-source-order", """
            const instance = Symbol("instance"), statik = Symbol("static"), events: string[] = [];
            function key(label: string, symbol: symbol) { events.push(label); return symbol; }
            class C<T> {
                static get [key("static", statik)]() { return 2; }
                get [key("instance", instance)]() { return 1; }
            }
            console.log(events.join(","));
            const first: any = new C<number>(), second: any = new C<string>(), constructor: any = C;
            console.log(events.join(","), constructor[statik], first[instance], second[instance]);
            """, "static,instance\nstatic,instance 2 1 1\n"];
        yield return ["mixed-method-accessor-and-field-key-slots", """
            const events: string[] = [];
            function key(label: string) { events.push(label); return label; }
            class C<T> {
                [key("field")] = 4;
                [key("method")]() { return 5; }
                get [key("getter")]() { return 6; }
                static initialized = (events.push("static-init"), 7);
            }
            console.log(events.join(","));
            const first: any = new C<number>(), second: any = new C<string>();
            console.log(events.join(","), first.field, second.field,
                first.method(), second.method(), first.getter, second.getter);
            """, "field,method,getter,static-init\nfield,method,getter,static-init 4 4 5 5 6 6\n"];
        yield return ["paired-accessors-retain-closed-owner-receivers", """
            const symbol = Symbol("state");
            let evaluations = 0;
            function key() { evaluations++; return symbol; }
            class C<T> {
                stored = 0;
                get [key()]() { return this.stored; }
                set [key()](value: number) { this.stored = value; }
            }
            const first: any = new C<number>(), second: any = new C<string>();
            first[symbol] = 11; second[symbol] = 22;
            const descriptor = Object.getOwnPropertyDescriptor(C.prototype, symbol)!;
            console.log(evaluations, first[symbol], second[symbol],
                descriptor.get!.call(first), descriptor.get!.call(second));
            descriptor.set!.call(first, 33);
            console.log(first[symbol], second[symbol], evaluations);
            """, "2 11 22 11 22\n33 22 2\n"];
        yield return ["property-key-coercion-is-not-replayed", """
            let coercions = 0;
            const key: any = { toString() { coercions++; return "result"; } };
            class C<T> { get [key]() { return 7; } }
            console.log(coercions);
            const first: any = new C<number>(), second: any = new C<string>();
            console.log(coercions, first.result, second.result);
            """, "1\n1 7 7\n"];
        yield return ["repeated-factory-definitions", """
            const symbol = Symbol("key");
            let evaluations = 0;
            function key() { evaluations++; return symbol; }
            function make() { class C<T> { get [key()]() { return 1; } } return C; }
            const First: any = make(), Second: any = make();
            const first: any = new First(), second: any = new Second();
            console.log(evaluations, first[symbol], second[symbol], First !== Second);
            """, "2 1 1 true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // Interpreter object-key coercion for class accessors is tracked in #2472.
            .Where(mode => mode == ExecutionMode.Compiled || (string)row[0] != "property-key-coercion-is-not-replayed")
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ComputedAccessorKeysEvaluateOncePerGuestDefinition(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
