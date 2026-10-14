using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassInstanceArgumentTests
{
    public const string Source = """
        class Inner {
            value: number;
            constructor(value: number) { this.value = value; }
        }
        class Outer {
            inner: Inner;
            constructor(inner: Inner) { this.inner = inner; }
            read(inner: Inner) { return inner.value; }
        }
        function read(inner: Inner) { return inner.value; }
        const outer = new Outer(new Inner(42));
        console.log(JSON.stringify(outer));
        console.log(read(new Inner(43)), outer.read(new Inner(44)));
        function* values() {
            yield read(new Inner(45));
            yield outer.read(new Inner(46));
            yield new Outer(new Inner(47)).inner.value;
        }
        for (const value of values()) console.log(value);
        const arrow = async () => read(new Inner(48));
        async function* asyncValues() {
            await Promise.resolve(0);
            yield outer.read(new Inner(49));
        }
        async function run() {
            await Promise.resolve(0);
            console.log(new Outer(new Inner(50)).inner.value, read(new Inner(51)), outer.read(new Inner(52)));
            console.log(await arrow(), (await asyncValues().next()).value);
        }
        run();
        """;

    public const string Expected = "{\"inner\":{\"value\":42}}\n43 44\n45\n46\n47\n50 51 52\n48 49\n";

    public const string ReplacementSource = """
        const replacement: any = { value: 42 };
        class Replaced {
            value = 1;
            constructor() { return replacement; }
        }
        class Holder {
            item: Replaced;
            constructor(item: Replaced) { this.item = item; }
            read(item: Replaced) { return item === replacement; }
        }
        function identity(item: Replaced): Replaced { return item; }
        const holder = new Holder(new Replaced());
        console.log(holder.item === replacement, identity(new Replaced()) === replacement, holder.read(new Replaced()));
        function* values() { yield identity(new Replaced()) === replacement; }
        console.log(values().next().value);
        async function run() { console.log(new Holder(new Replaced()).item === replacement, identity(new Replaced()) === replacement); }
        run();
        """;

    public const string ReplacementExpected = "true true true\ntrue\ntrue true\n";

    [Theory, ModeData]
    public void ConstructorFunctionAndMethodArgumentsRetainOrdinaryClassReferences(ExecutionMode mode)
        => Assert.Equal(Expected, TestHarness.Run(Source, mode));

    [Theory, ModeData]
    public void ReplacementCapableClassArgumentsRetainObjectSlots(ExecutionMode mode)
        => Assert.Equal(ReplacementExpected, TestHarness.Run(ReplacementSource, mode));
}
