using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class SuperInstanceAccessorTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class B { get value(): number { return 7; } }
            class D extends B { read() { return super.value; } }
            console.log(new D().read());
            """, "7\n"];
        yield return ["receiver-and-arrows", """
            let calls = 0, receiver: any;
            class Base {
                saved = 7;
                get value(): number { calls++; receiver = this; return this.saved; }
            }
            class Derived extends Base {
                read(): number { return super.value; }
                arrow() { return () => super.value; }
            }
            class Leaf extends Derived {}
            const leaf = new Leaf();
            leaf.saved = 19;
            console.log(leaf.read(), leaf.arrow()(), calls, receiver === leaf);
            leaf.saved = 23;
            console.log(leaf.arrow()(), calls, receiver === leaf);
            """, "19 19 2 true\n23 3 true\n"];
        yield return ["inferred-inheritance", """
            class Base { get value() { return 11; } }
            class Middle extends Base {}
            class Derived extends Middle {
                read(): number { return super.value; }
                arrow(): () => number { return () => super.value; }
            }
            console.log(new Derived().read(), new Derived().arrow()());
            """, "11 11\n"];
        yield return ["generic-inheritance", """
            class Base<T> {
                saved: T;
                constructor(value: T) { this.saved = value; }
                protected get value(): T { return this.saved; }
            }
            // Explicit forwarding avoids the independent implicit-constructor
            // substitution defect tracked by #2206.
            class Middle<U> extends Base<U> { constructor(value: U) { super(value); } }
            class Derived extends Middle<number> {
                constructor(value: number) { super(value); }
                read(): number { return super.value; }
                arrow(): () => number { return () => super.value; }
            }
            const value = new Derived(13);
            console.log(value.read(), value.arrow()());
            """, "13 13\n"];
        yield return ["getter-throw", """
            const failure = { value: 17 };
            let calls = 0;
            class Base { get value(): number { calls++; throw failure; } }
            class Derived extends Base { read() { return super.value; } }
            try { new Derived().read(); } catch (error) { console.log(error === failure, calls); }
            """, "true 1\n"];
        yield return ["setter-only-shadow", """
            let reads = 0;
            class Base { get value(): any { reads++; return 1; } }
            class Middle extends Base { set value(next: number) {} }
            // Keep the returned sentinel in an object slot; #2234 tracks
            // numeric return coercion independently of accessor resolution.
            class Derived extends Middle { read(): any { return super.value; } }
            console.log(new Derived().read() === undefined, reads);
            """, "true 0\n"];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void CheckedSuperReadsUseTheNearestAccessorAndActualReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
