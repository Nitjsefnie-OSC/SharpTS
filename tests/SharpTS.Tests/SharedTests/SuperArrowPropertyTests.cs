using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class SuperArrowPropertyTests
{
    public const string GenericSource = """
        class A<T> { value(prefix: string = "generic", ...rest: number[]): string { return prefix + ":" + rest.join(","); } }
        class B<T> extends A<T> { read(): any { return (() => super.value)(); } }
        const b = new B<number>(), method: any = b.read();
        console.log(method.call(b), method.call(b, "given", 1, 2), Object.hasOwn(method, "prototype"));
        class GeneratorA<T> { *values() { yield "base"; } }
        class GeneratorB<T> extends GeneratorA<T> { read(): any { return (() => super.values)(); } }
        const generator = new GeneratorB<number>(), values: any = generator.read();
        console.log(Object.hasOwn(values, "prototype"), values.call(generator).next().value);
        """;
    public const string GenericExpected = "generic: given:1,2 false\ntrue base\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class A {}
            class B extends A { read() { return (() => super.value)(); } }
            A.prototype.value = "a";
            console.log(B.prototype.read(), new B().read());
            """, "a a\n"];
        yield return ["live-values", """
            class A {}
            class B extends A { read() { return () => super.value; } }
            const read = new B().read();
            A.prototype.value = "first";
            console.log(read());
            A.prototype.value = null;
            console.log(read() === null);
            A.prototype.value = undefined;
            console.log(read() === undefined);
            delete A.prototype.value;
            console.log(read() === undefined);
            Object.defineProperty(A.prototype, "value", { value: "descriptor", configurable: true });
            console.log(read());
            """, "first\ntrue\ntrue\ntrue\ndescriptor\n"];
        yield return ["own-field", """
            class A { value = "instance"; }
            class B extends A { read() { return (() => super.value)(); } }
            const b = new B();
            console.log(b.value, b.read() === undefined);
            Object.defineProperty(A.prototype, "value", { value: "prototype", configurable: true });
            console.log(b.value, b.read());
            """, "instance true\ninstance prototype\n"];
        yield return ["receiver", """
            class A {}
            class B extends A { read() { return () => (() => super.value)(); } }
            class C extends B {}
            Object.defineProperty(A.prototype, "value", {
                get() { return this.tag; }, configurable: true
            });
            Object.defineProperty(B.prototype, "value", { value: "wrong-B", configurable: true });
            Object.defineProperty(C.prototype, "value", { value: "wrong-C", configurable: true });
            const child = new C();
            child.tag = "child";
            const read = child.read();
            console.log(read.call({ tag: "wrong-call" }));
            child.tag = "changed";
            console.log(read());
            """, "child\nchanged\n"];
        yield return ["shadowing", """
            let reads = 0;
            class Root {}
            class A extends Root {}
            class B extends A { read() { return (() => super.value)(); } }
            Object.defineProperty(Root.prototype, "value", {
                get() { reads++; return "root"; }, configurable: true
            });
            const b = new B();
            Object.defineProperty(A.prototype, "value", { set(value) {}, configurable: true });
            console.log(b.read() === undefined, reads);
            Object.defineProperty(A.prototype, "value", { get: undefined, set(value) {}, configurable: true });
            console.log(b.read() === undefined, reads);
            Object.defineProperty(A.prototype, "value", { value: undefined, configurable: true });
            console.log(b.read() === undefined, reads);
            Object.defineProperty(A.prototype, "value", { value: null, configurable: true });
            console.log(b.read() === null, reads);
            delete A.prototype.value;
            console.log(b.read(), reads);
            """, "true 0\ntrue 0\ntrue 0\ntrue 0\nroot 1\n"];
        yield return ["method-value", """
            class A { value() { return "base"; } }
            class B extends A {
                value() { return "derived"; }
                read() { const method = (() => super.value)(); return method.call(this); }
            }
            const b = new B();
            b.tag = "receiver";
            console.log(b.read());
            A.prototype.value = function() { return "replacement:" + this.tag; };
            console.log(b.read());
            class Parent {
                optional(value) { return value === undefined; }
            }
            class Child extends Parent {
                readOptional() { return (() => super.optional)(); }
            }
            const child = new Child(), method = child.readOptional();
            console.log(method(), Object.hasOwn(method, "prototype"));
            try { new method(); } catch (error) { console.log(error instanceof TypeError); }
            """, "base\nreplacement:receiver\ntrue false\ntrue\n"];
        yield return ["expression", """
            class A {}
            const B = class extends A { read() { return (() => super.value)(); } };
            A.prototype.value = "expression";
            console.log(new B().read());
            """, "expression\n"];
        yield return ["generator-value", """
            class A { *values() { yield "base"; } }
            class B extends A { read() { return (() => super.values)(); } }
            const b = new B(), method = b.read();
            console.log(Object.hasOwn(method, "prototype"), method.call(b).next().value);
            """, "true base\n"];
        yield return ["suspension", """
            class A {}
            class B extends A {
                async read() {
                    const read = async () => { await Promise.resolve(); return super.value; };
                    return await read();
                }
                *values() { yield (() => super.value)(); }
            }
            A.prototype.value = "suspended";
            const b = new B();
            console.log(b.values().next().value);
            b.read().then(value => console.log(value));
            """, "suspended\nsuspended\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ReadsLiveLexicalPrototypeWithArrowReceiver(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunUnchecked(source, mode));
    }

    [Theory]
    [InlineData(ExecutionMode.Interpreted)]
    [InlineData(ExecutionMode.Compiled)]
    public void GenericMethodValuePreservesCallingConvention(ExecutionMode mode)
        => Assert.Equal(GenericExpected, TestHarness.Run(GenericSource, mode));

    [Fact]
    public void BorrowedArrowKeepsReceiverInInterpreter()
    {
        // Compiled ordinary method borrowing has the independent #2180 receiver
        // limitation even without a super expression.
        const string source = """
            class A {}
            class B extends A { read() { return () => (() => super.value)(); } }
            Object.defineProperty(A.prototype, "value", { get() { return this.tag; } });
            const receiver = { tag: "borrowed" };
            const read = B.prototype.read.call(receiver);
            console.log(read.call({ tag: "wrong-call" }));
            receiver.tag = "changed";
            console.log(read());
            """;
        Assert.Equal("borrowed\nchanged\n", TestHarness.RunUnchecked(source, ExecutionMode.Interpreted));
    }
}
