using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class TypedCallableFieldTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["constructor-assignment", """
            class C {
                read: () => number;
                constructor() { this.read = () => 7; }
            }
            console.log(new C().read());
            """, "7\n"];
        yield return ["generic-class", """
            class Payload {}
            class C<T> {
                read: () => number;
                payload: Payload;
                constructor() { this.read = () => 7; this.payload = new Payload(); }
            }
            const number = new C<number>(), text = new C<string>();
            console.log(number.read(), text.read(), number.payload instanceof Payload);
            """, "7 7 true\n"];
        yield return ["initializer-reassignment", """
            class C { read: () => number = () => 7; }
            const value = new C();
            console.log(value.read());
            value.read = () => 9;
            console.log(value.read());
            """, "7\n9\n"];
        yield return ["function-value", """
            function answer(): number { return 7; }
            class C {
                read: () => number;
                constructor() { this.read = answer; }
            }
            console.log(new C().read());
            """, "7\n"];
        yield return ["function-type-alias", """
            type Reader = () => number;
            class C { read: Reader = () => 7; }
            console.log(new C().read());
            """, "7\n"];
        yield return ["nullable-callable", """
            class C { read: (() => number) | null = null; }
            const value = new C();
            console.log(value.read === null);
            value.read = () => 7;
            const read: any = value.read;
            console.log(read());
            value.read = null;
            console.log(value.read === null);
            """, "true\n7\ntrue\n"];
        yield return ["existing-storage-controls", """
            class C {
                read: any = () => 7;
                static read: () => number = () => 8;
                #private: () => number = () => 9;
                run() { return this.#private(); }
            }
            class Inferred { read = () => 10; }
            const Expression = class { read: () => number = () => 11; };
            console.log(new C().read(), C.read(), new C().run(), new Inferred().read(), new Expression().read());
            """, "7 8 9 10 11\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void TypedFieldsPreserveGuestCallableValues(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
