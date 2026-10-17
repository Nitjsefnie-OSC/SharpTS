using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionStaticSelfTests
{
    public const string OriginalSource = """
        const C = class Named {
            static get self() { return Named; }
            static value = this.self;
        };
        console.log(C.value === C);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", OriginalSource, "true\n"];
        yield return ["getter-method-and-block", """
            let reads = 0;
            const events: string[] = [];
            const C = class Named {
                static get self() { reads++; events.push("get"); return Named; }
                static method() { events.push("method"); return Named; }
                static fromGetter = this.self;
                static fromMethod = this.method();
                static fromBlock: any;
                static { events.push("block"); this.fromBlock = this.self; }
            };
            console.log(C.fromGetter === C, C.fromMethod === C, C.fromBlock === C);
            console.log(reads, events.join(","));
            """, "true true true\n2 get,method,block,get\n"];
        yield return ["repeated-factory-and-outer-binding", """
            const Named = 17;
            function make(value: number) {
                return class Named {
                    static get self() { return Named; }
                    static value = this.self;
                    static captured = value;
                    static pair: any = [this.self, value];
                    static read(): any { return [Named, value]; }
                };
            }
            const First = make(41), Second = make(42);
            console.log(Named, First.value === First, Second.value === Second, First !== Second);
            console.log(First.captured, Second.captured, First.pair[0] === First, Second.pair[0] === Second);
            const one = First.read(), two = Second.read();
            console.log(one[0] === First, one[1], two[0] === Second, two[1]);
            """, "17 true true true\n41 42 true true\ntrue 41 true 42\n"];
        yield return ["escaped-initializer-arrows", """
            const saved: any[] = [];
            function make() {
                return class Named {
                    static get self() { return () => Named; }
                    static escaped = this.self;
                    static during = this.escaped();
                    static { saved.push(this.self); }
                };
            }
            const First = make(), Second = make();
            console.log(First.during === First, Second.during === Second);
            console.log(First.escaped() === First, Second.escaped() === Second,
                saved[0]() === First, saved[1]() === Second);
            console.log(saved[0].call(Second) === First);
            """, "true true\ntrue true true true\ntrue\n"];
        yield return ["derived-factory-super-frame", """
            class Base { static marker = "base"; }
            function make(value: number) {
                return class Named extends Base {
                    static get self() { return Named; }
                    static get details(): any { return [Named, value, super.marker]; }
                    static owner = this.self;
                    static snapshot = this.details;
                    static blockOwner: any;
                    static { this.blockOwner = this.self; }
                };
            }
            const First = make(53), Second = make(59);
            console.log(First.owner === First, Second.owner === Second,
                First.blockOwner === First, Second.blockOwner === Second);
            console.log(First.snapshot[0] === First, First.snapshot[1], First.snapshot[2],
                Second.snapshot[0] === Second, Second.snapshot[1], Second.snapshot[2]);
            console.log(Object.getPrototypeOf(First) === Base, Object.getPrototypeOf(Second) === Base);
            """, "true true true true\ntrue 53 base true 59 base\ntrue true\n"];
        yield return ["nested-self-private-and-order", """
            const events: string[] = [];
            function mark(value: number) { events.push("field:" + value); return value; }
            const Outer = class Named {
                static #secret = mark(61);
                static get self() { events.push("getter"); return Named; }
                static first = this.self;
                static Inner = class Inner {
                    static get self() { return Inner; }
                    static own = this.self;
                    static get outer() { return Named; }
                    static owner = this.outer;
                    static get secret() { return Named.#secret; }
                    static value = this.secret;
                };
                static last = mark(67);
                static { events.push("block"); }
            };
            console.log(Outer.first === Outer, Outer.Inner.own === Outer.Inner,
                Outer.Inner.owner === Outer, Outer.Inner.value, Outer.last);
            console.log(events.join(","));
            """, "true true true 61 67\nfield:61,getter,field:67,block\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, ModeData]
    public void OriginalSourceHasSelfBindingDuringStaticInitialization(ExecutionMode mode)
        => Assert.Equal("true\n", TestHarness.Run(OriginalSource, mode));

    [Theory, MemberData(nameof(ParityCases))]
    public void NamedSelfIsAvailableBeforeStaticInitialization(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
