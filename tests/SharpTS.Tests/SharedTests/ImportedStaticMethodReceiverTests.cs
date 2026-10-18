using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ImportedStaticMethodReceiverTests
{
    private const string OwnerSource = """
        class Other { static who(): any { return this; } }
        class C {
            static who(): any { return this; }
            static get value(): any { return (this as any).slot; }
            static read(): any { return this.value; }
            static nested(): any {
                const before = this;
                const other = Other.who();
                console.log("nested", before === this, other === Other);
                return this;
            }
            static pack(first: any, optional?: string, ...tail: any[]): any {
                console.log("pack", this === C, first, optional === undefined,
                    tail.length, tail[0], arguments.length);
                return this;
            }
            static fixed(first: any, ...tail: any[]): any;
            static fixed(first: any): any {
                console.log("fixed", this === C, first, arguments.length, arguments[1]);
                return this;
            }
            static defaults(value: string = "fallback"): any {
                console.log("default", this === C, value);
                return this;
            }
            static lookup(value: any): any { return this; }
            static fail(): any {
                console.log("throw", this === C);
                throw new Error("boom");
            }
            static number(value: number): number { return this === C ? value + 1 : -1; }
        }
        export = C;
        """;

    public static Dictionary<string, string> Files(string entry) => new()
    {
        ["owner.ts"] = OwnerSource,
        ["inherited.ts"] = """
            class Base<T> { static who(): any { return this; } }
            class C<T> extends Base<T> {}
            export = C;
            """,
        ["generic.ts"] = """
            class C<T> { static who(): any { return this; } }
            export = C;
            """,
        ["main.ts"] = entry
    };

    public static IEnumerable<object[]> Cases()
    {
        yield return ["constructor-identities", """
            import Owner = require('./owner');
            import Inherited = require('./inherited');
            import Generic = require('./generic');
            console.log(Owner.who() === Owner, Inherited.who() === Inherited,
                Generic.who() === Generic);
            const owner: any = Owner, inherited: any = Inherited, generic: any = Generic;
            console.log(owner !== inherited, owner !== generic, inherited !== generic);
            owner.slot = 19;
            console.log(Owner.value, Owner.read());
            console.log("nested-result", Owner.nested() === Owner);
            """, "true true true\ntrue true true\n19 19\nnested true true\nnested-result true\n"];
        yield return ["arguments-lookup-and-cleanup", """
            import Owner = require('./owner');
            let trace = "";
            function argument(): number {
                trace += "arg,";
                Object.defineProperty(Owner, "lookup", { value: () => false, configurable: true });
                return 7;
            }
            class Caller {
                static run(): any {
                    console.log("argument-this", Owner.pack(this === Caller) === Owner);
                    Owner.pack("first", undefined, "tail", 7);
                    const spread: [any, string, any, any] = ["spread", "present", 8, 9];
                    Owner.pack(...spread);
                    Owner.fixed("first", this === Caller);
                    Owner.defaults(); Owner.defaults(undefined); Owner.defaults("set");
                    Object.defineProperty(Owner, "lookup", {
                        get: function(this: any) {
                            trace += "get,";
                            return function(this: any, value: any) {
                                trace += "call,";
                                console.log("lookup-this", this === Owner, value);
                                return this;
                            };
                        }, configurable: true
                    });
                    console.log("lookup-result", Owner.lookup(argument()) === Owner, trace);
                    try { Owner.fail(); }
                    catch (error: any) { console.log("caught", error.message, this === Caller); }
                    return this;
                }
            }
            console.log("caller", Caller.run() === Caller);
            """, "pack true true true 0 undefined 1\nargument-this true\npack true first true 2 tail 4\npack true spread false 2 8 4\nfixed true first 2 true\ndefault true fallback\ndefault true fallback\ndefault true set\nlookup-this true 7\nlookup-result true get,arg,call,\nthrow true\ncaught boom true\ncaller true\n"];
        yield return ["pending-arguments", """
            import Owner = require('./owner');
            async function run(): Promise<void> {
                const pending = new Promise<any>(resolve => setTimeout(() => resolve("ready"), 0));
                console.log("pending", Owner.pack(await pending, undefined,
                    await Promise.resolve("tail")) === Owner);
                console.log("numeric", Owner.number(await Promise.resolve(4)) + 1);
            }
            run();
            """, "pack true ready true 1 tail 3\npending true\nnumeric 6\n"];
        yield return ["yield-retains-resolved-callable", """
            import Owner = require('./owner');
            function* run(): Generator<any, void, any> {
                console.log("yield-result", Owner.pack(yield "pause") === Owner);
            }
            const iterator = run();
            console.log(iterator.next().value);
            Object.defineProperty(Owner, "pack", {
                value: function(this: any, value: any) {
                    console.log("replacement", value);
                    return false;
                }, configurable: true
            });
            console.log(iterator.next(7).done);
            """, "pause\npack true 7 true 0 undefined 1\nyield-result true\ntrue\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ImportedStaticCallsRetainTheirConstructorReceiver(
        string name, string entry, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(Files(entry), "main.ts", mode));
    }
}
