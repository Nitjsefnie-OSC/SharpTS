using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InstanceMethodIdentityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class C {
                value: number;
                constructor(value: number) { this.value = value; }
                read(extra: number = 0) { return this.value + extra; }
            }
            const first: any = new C(7), second: any = new C(11);
            const prototype: any = C.prototype;
            const method: any = first.read;
            console.log(method === first.read, method === second.read, method === prototype.read);
            console.log(method.call(first, 1), method.call(second, 2), method.apply(second, [3]));
            const bound: any = method.bind(first, 4);
            console.log(bound(), bound.call(second), bound.bind(second)());
            console.log(method.call(second, 5), first.read());
            """, "true true true\n8 13 14\n11 11 11\n16 7\n"];
        yield return ["inherited-and-overridden", """
            class Base { value = 7; read() { return this.value; } }
            class Derived extends Base { value = 11; }
            class Override extends Base { read() { return this.value + 1; } }
            const first: any = new Base(), second: any = new Derived(), third: any = new Override();
            console.log(first.read === second.read, second.read === Base.prototype.read);
            console.log(third.read === Override.prototype.read, third.read !== first.read);
            console.log(first.read(), second.read(), third.read(), first.read.call(third));
            """, "true true\ntrue true\n7 11 8 7\n"];
        yield return ["method-metadata-is-shared", """
            class C { read(value: number) { return value; } }
            const first: any = new C(), second: any = new C();
            first.read.marker = 42;
            console.log(second.read.marker, (C.prototype.read as any).marker);
            console.log(first.read.name, first.read.length, "prototype" in first.read);
            """, "42 42\nread 1 false\n"];
        yield return ["detached-method-is-unbound", """
            class C { value = 7; read() { return this.value; } }
            const instance: any = new C(), method: any = instance.read;
            try { method(); } catch (error: any) { console.log(error instanceof TypeError); }
            console.log(instance.read(), method.call({ value: 11 }));
            """, "true\n7 11\n"];
        yield return ["async-and-generator-methods", """
            class C {
                value = 7;
                async read() { await Promise.resolve(0); return this.value; }
                *values() { yield this.value; }
            }
            const first: any = new C(), second: any = new C();
            second.value = 11;
            console.log(first.read === second.read, first.read === C.prototype.read,
                first.values === second.values, first.values === C.prototype.values);
            async function run() {
                console.log(await first.read(), await second.read(),
                    first.values().next().value, second.values().next().value);
            }
            run();
            """, "true true true true\n7 11 7 11\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        // #2197 tracks the shared compiled guest-this null/undefined read guard.
        => Cases().SelectMany(row => ((string)row[0] == "detached-method-is-unbound"
                ? new[] { ExecutionMode.Interpreted }
                : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void InstanceMethodReadsPreserveTheSharedPrototypeFunction(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
