using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AccessorVisibilityRuntimeTests
{
    public const string StaticReadOrdersSource = """
        class First { public static get value(): number { return 1; } private static set value(next: number) {} }
        class Last { private static set value(next: number) {} public static get value(): number { return 2; } }
        class ProtectedFirst { public static get value(): number { return 3; } protected static set value(next: number) {} }
        class ProtectedLast { protected static set value(next: number) {} public static get value(): number { return 4; } }
        class GenericFirst<T> { public static get value(): number { return 5; } private static set value(next: number) {} }
        class GenericLast<T> { private static set value(next: number) {} public static get value(): number { return 6; } }
        const ExpressionFirst = class { public static get value(): number { return 7; } private static set value(next: number) {} };
        const ExpressionLast = class { private static set value(next: number) {} public static get value(): number { return 8; } };
        const first: number = First.value, last: number = Last.value;
        const protectedFirst: number = ProtectedFirst.value, protectedLast: number = ProtectedLast.value;
        const genericFirst: number = GenericFirst.value, genericLast: number = GenericLast.value;
        const expressionFirst: number = ExpressionFirst.value, expressionLast: number = ExpressionLast.value;
        console.log(first, last, protectedFirst, protectedLast, genericFirst, genericLast, expressionFirst, expressionLast);
        """;

    public const string PermittedOwnersSource = """
        const stored = { instance: 1, statik: 2 };
        class Owner {
            private get value(): number { return stored.instance; }
            private set value(next: number) { stored.instance = next; }
            change(next: number): number { const write = () => { this.value = next; }; write(); return this.value; }
            public static get value(): number { return stored.statik; }
            private static set value(next: number) { stored.statik = next; }
            static change(next: number): number { const write = () => { this.value = next; }; write(); return this.value; }
        }
        class Root {
            slot = 3;
            protected get value(): number { return this.slot; }
            protected set value(next: number) { this.slot = next; }
        }
        class Derived extends Root {
            change(other: Derived): number { this.value = 5; other.value = 7; return this.value + other.value; }
        }
        console.log(new Owner().change(9), Owner.change(11), new Derived().change(new Derived()));
        """;

    public const string EscapesAndNearerPublicSource = """
        const state = { last: 0, writes: 0 };
        class C {
            private get secret(): number { return 7; }
            private set secret(next: number) { state.last = next; state.writes++; }
        }
        const typed = new C(), dynamic: any = typed;
        dynamic.secret = 8;
        typed["secret"] = 9;
        console.log(dynamic.secret, typed["secret"], state.last, state.writes);
        const Expression = class { private get secret(): number { return state.last + 1; } };
        const expression = new Expression(), dynamicExpression: any = expression;
        console.log(dynamicExpression.secret, expression["secret"]);
        class Far { protected get value(): number { return 1; } }
        class Near extends Far { public get value(): number { return 2; } }
        const value: number = new Near().value;
        console.log(value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["static-read-order-and-visibility-facets", StaticReadOrdersSource, "1 2 3 4 5 6 7 8\n"];
        yield return ["same-owner-arrows-and-protected-derived-receivers", PermittedOwnersSource, "9 11 12\n"];
        yield return ["any-bracket-escapes-and-nearer-public-getter", EscapesAndNearerPublicSource, "7 7 9 2\n10 10\n2\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void PermittedAccessorOperationsRetainTheirRuntimeBehavior(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
