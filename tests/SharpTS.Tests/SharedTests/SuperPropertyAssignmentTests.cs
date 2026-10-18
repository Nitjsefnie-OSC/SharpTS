using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class SuperPropertyAssignmentTests
{
    [Theory, ModeData]
    public void StaticAccessorAssignmentPreservesTheDerivedReceiver(ExecutionMode mode)
    {
        const string source = """
            class Base {
                static stored = 1;
                static get value(): number { return this.stored; }
                static set value(next: number) { this.stored = next; }
            }
            class Child extends Base {
                static get value(): number { return super.value * 2; }
                static set value(next: number) { super.value = next + 1; }
            }
            Child.value = 4;
            console.log(Child.value, Base.value);
            """;
        Assert.Equal("10 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void InheritedSetterOnlyAssignmentReturnsTheRhs(ExecutionMode mode)
    {
        const string source = """
            class Base { stored = 1; set value(next: number) { this.stored = next; } }
            class Middle extends Base { write(next: number) { return (super.value) = next; } }
            class Child extends Middle {}
            const child = new Child();
            console.log(child.write(7), child.stored);
            """;
        Assert.Equal("7 7\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AssignmentUsesTheSetterTypeAndGenericSubstitutions(ExecutionMode mode)
    {
        const string source = """
            class Base<T> {
                stored: T;
                constructor(initial: T) { this.stored = initial; }
                get value(): any { return "value"; }
                set value(next: T) { this.stored = next; }
            }
            class Child extends Base<number> {
                write(next: number) { const assign = () => super.value = next; return assign(); }
            }
            const child = new Child(1);
            console.log(child.write(9), child.stored);
            """;
        Assert.Equal("9 9\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void BorrowedMethodRetainsItsLexicalHome(ExecutionMode mode)
    {
        const string source = """
            class Base { set value(next: number) { (this as any).stored = next + 1; } }
            class Child extends Base { write(next: number) { return super.value = next; } }
            const write: any = new Child().write;
            const receiver: any = { stored: 0 };
            console.log(write.call(receiver, 7), receiver.stored);
            """;
        Assert.Equal("7 8\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AssignmentCapturesTheSuperBaseBeforeTheRhs(ExecutionMode mode)
    {
        const string source = """
            class Base { set value(next: number) { console.log("original", next); } }
            class Child extends Base { write() { return super.value = change(); } }
            function change() {
                Object.setPrototypeOf(Child.prototype, { set value(next: number) { console.log("new", next); } });
                return 5;
            }
            console.log(new Child().write());
            """;
        Assert.Equal("original 5\n5\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void SuspendedRhsRetainsTheSuperReference(ExecutionMode mode)
    {
        const string source = """
            class Base { stored = 0; set value(next: number) { this.stored = next; } }
            class Child extends Base {
                async write(next: number) { return super.value = await Promise.resolve(next); }
                *writeGenerator() { return super.value = yield 3; }
            }
            async function main() {
                const child = new Child();
                console.log(await child.write(7), child.stored);
                const iterator = child.writeGenerator();
                console.log(iterator.next().value);
                console.log(iterator.next(9).value, child.stored);
            }
            main();
            """;
        Assert.Equal("7 7\n3\n9 9\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ProtectedSetterAndStaticDataWritesKeepTheChildReceiver(ExecutionMode mode)
    {
        const string source = """
            class Base {
                stored = 0;
                protected set value(next: number) { this.stored = next; }
                static count = 1;
            }
            class Child extends Base {
                write() { super.value = 5; return this.stored; }
                static write() { return super.count = 7; }
            }
            console.log(new Child().write(), Child.write(), Child.count, Base.count);
            """;
        Assert.Equal("5 7 7 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void RuntimeRefusalThrowsTypeErrorAfterEvaluatingTheRhs(ExecutionMode mode)
    {
        const string source = """
            let calls = 0;
            class Base { set value(next: number) {} }
            class Child extends Base { write() { return super.value = ++calls; } }
            Object.defineProperty(Base.prototype, "value", { value: 1, writable: false });
            try { new Child().write(); } catch (error) { console.log(error instanceof TypeError, calls); }
            """;
        Assert.Equal("true 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void GetterOnlyAndIncompatibleAssignmentsAreRejected(ExecutionMode mode)
    {
        const string readonlySource = """
            class Base { get value(): number { return 1; } }
            class Child extends Base { write() { super.value = 2; } }
            """;
        Assert.Contains("read-only", Assert.ThrowsAny<Exception>(() => TestHarness.Run(readonlySource, mode)).Message);
        const string incompatibleSource = """
            class Base { set value(next: number) {} }
            class Child extends Base { write() { super.value = "wrong"; } }
            """;
        Assert.Contains("Cannot assign", Assert.ThrowsAny<Exception>(() => TestHarness.Run(incompatibleSource, mode)).Message);
        const string privateSource = """
            class Base { private set value(next: number) {} }
            class Child extends Base { write() { super.value = 2; } }
            """;
        Assert.Contains("private", Assert.ThrowsAny<Exception>(() => TestHarness.Run(privateSource, mode)).Message);
        const string staticReadonlySource = """
            class Base { static readonly value = 1; }
            class Child extends Base { static write() { super.value = 2; } }
            """;
        Assert.Contains("read-only", Assert.ThrowsAny<Exception>(() => TestHarness.Run(staticReadonlySource, mode)).Message);
    }
}
