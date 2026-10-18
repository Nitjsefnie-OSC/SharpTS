using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticCallReceiverRestorationTests
{
    public const string Original = """
        class Other { static fail(): void { throw new Error("boom"); } }
        class Caller {
            static run(): any {
                console.log("before", this === Caller);
                try { Other.fail(); }
                catch (error) { const receiver: any = this; console.log("caught", receiver === Caller, receiver === Other); }
                return this;
            }
        }
        console.log("after", Caller.run() === Caller);
        """;

    public const string DynamicControl = """
        class Other { static fail(): void { throw new Error("boom"); } }
        class Caller {
            static run(): any {
                console.log("before", this === Caller);
                try { const target: any = Other; target.fail(); }
                catch (error) { const receiver: any = this; console.log("caught", receiver === Caller, receiver === Other); }
                return this;
            }
        }
        console.log("after", Caller.run() === Caller);
        """;

    public const string NonthrowingControl = """
        class Other { static ok(): void {} }
        class Caller {
            static run(): any {
                console.log("before", this === Caller);
                try { Other.ok(); }
                catch (error) { console.log("unexpected"); }
                console.log("restored", this === Caller, (this as any) === Other);
                return this;
            }
        }
        console.log("after", Caller.run() === Caller);
        """;

    public const string ArgumentsAndSuspension = """
        const trace: string[] = [];
        class Other {
            static get(value: number, fail: boolean, ...tail: number[]): number {
                trace.push("body");
                if (fail) throw new Error("boom");
                return value + tail[0];
            }
        }
        class Caller {
            static argument(): number { trace.push("argument"); throw new Error("arg"); }
            static run(): void {
                try { const values = [100, Other.get(2, true, ...[3])]; console.log(values); }
                catch { console.log("caught", this === Caller); }
                try { Other.get(Caller.argument(), false, ...[3]); }
                catch { console.log("argument", this === Caller); }
                console.log("typed", 10 + Other.get(2, false, ...[3]));
            }
            static async pending(value: Promise<number>): Promise<void> {
                const nested = async () => {
                    trace.push("arg");
                    const result = Other.get(await value, false, ...[4]);
                    console.log("async", result, this === Caller);
                };
                await nested();
            }
        }
        Caller.run();
        let release: (value: number) => void = () => {};
        const pending = new Promise<number>(resolve => { release = resolve; });
        const task = Caller.pending(pending);
        console.log("paused", trace.join(","));
        release(3);
        task.then(() => console.log("done", trace.join(",")));
        """;

    public const string GenericInheritedReceivers = """
        class GenericBase<T> {
            static value(): any { return this; }
            static fail(): void { throw new Error("generic"); }
        }
        class NumberOwner extends GenericBase<number> {}
        class StringOwner extends GenericBase<string> {}
        class Caller {
            static run(): void {
                console.log("owners", NumberOwner.value() === NumberOwner,
                    StringOwner.value() === StringOwner);
                try { NumberOwner.fail(); }
                catch { console.log("number", this === Caller); }
                try { StringOwner.fail(); }
                catch { console.log("string", this === Caller); }
                console.log("after", this === Caller);
            }
        }
        Caller.run();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-throw", Original, "before true\ncaught true false\nafter true\n"];
        yield return ["dynamic-control", DynamicControl, "before true\ncaught true false\nafter true\n"];
        yield return ["nonthrowing-control", NonthrowingControl, "before true\nrestored true false\nafter true\n"];
        yield return ["arguments-and-suspension", ArgumentsAndSuspension, "caught true\nargument true\ntyped 15\npaused body,argument,body,arg\nasync 7 true\ndone body,argument,body,arg,body\n"];
        yield return ["generic-inherited-receivers", GenericInheritedReceivers, "owners true true\nnumber true\nstring true\nafter true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DirectStaticCallsRestoreTheEnclosingReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
