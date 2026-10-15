using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticSetterReceiverTests
{
    public const string IssueSource = """
        class Base {
            static stored: number = 0;
            static set value(value: number) { this.stored = value; }
        }
        class Derived extends Base {}
        Derived.value = 7;
        console.log(Base.stored, Derived.stored, Object.hasOwn(Derived, "stored"));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "0 7 true\n"];
        yield return ["direct-any-cast-body", """
            class C {
                static slot = 0;
                static set value(next: number) { (this as any).slot = next; }
            }
            C.value = 42;
            console.log(C.slot);
            """, "42\n"];
        yield return ["own-static-field-alias-storage", """
            class C { static slot = 0; }
            const alias: any = C;
            alias.slot = 42;
            console.log(C.slot, alias.slot);
            function strictWrite(target: any, next: number) {
                "use strict";
                target.slot = next;
            }
            strictWrite(C, 77);
            console.log(C.slot, alias.slot, Object.hasOwn(C, "slot"));
            Object.defineProperty(alias, "slot", { value: 1, writable: true, configurable: true });
            delete alias.slot;
            alias.slot = 42;
            console.log(alias.slot, Object.hasOwn(alias, "slot"));
            """, "42 42\n77 77 true\n42 true\n"];
        yield return ["sibling-deep-shadows-and-assignment-result", """
            class Base {
                static stored = 0;
                static set value(next: number) { this.stored = next; }
            }
            class Left extends Base {}
            class Right extends Base {}
            class Deep extends Left {}
            const assigned = Left.value = 3;
            Deep.value = 5; Right.value = 7; Base.value = 11;
            console.log(Base.stored, Left.stored, Right.stored, Deep.stored, assigned);
            console.log(Object.hasOwn(Left, "stored"), Object.hasOwn(Right, "stored"),
                Object.hasOwn(Deep, "stored"), Object.hasOwn(Left, "value"), Object.hasOwn(Deep, "value"));
            const created: any = Object.getOwnPropertyDescriptor(Left, "stored");
            console.log(created.value, created.writable, created.enumerable, created.configurable);
            Object.defineProperty(Deep, "stored", { value: 9, writable: true, enumerable: false, configurable: true });
            Deep.value = 13;
            const updated: any = Object.getOwnPropertyDescriptor(Deep, "stored");
            console.log(Base.stored, Left.stored, Deep.stored, updated.writable, updated.enumerable, updated.configurable);
            """, "11 3 7 5 3\ntrue true true false false\n3 true true true\n11 3 13 true false true\n"];
        yield return ["setter-body-operators-and-escaped-arrow", """
            const state: any = { update: null };
            class Base {
                static stored = 1;
                static marker(): string { return "base"; }
                static set increase(amount: number) {
                    console.log(this?.marker(), this === Derived);
                    const sum = this.stored += amount;
                    const before = this.stored++;
                    const after = ++this.stored;
                    state.update = () => this.stored += 1;
                    console.log(this === Derived, sum, before, after);
                }
            }
            class Derived extends Base {
                static marker(): string { return this === Derived ? "derived" : "wrong receiver"; }
            }
            Derived.increase = 2;
            console.log(Base.stored, Derived.stored);
            console.log(state.update(), Base.stored, Derived.stored);
            """, "derived true\ntrue 3 3 5\n1 5\n6 1 6\n"];
        yield return ["generic-base-source-setter", """
            class Base<T> {
                static stored = 0;
                static set value(next: number) { this.stored = next; }
            }
            class Plain extends Base<number> {}
            class Generic<T> extends Base<T> {}
            Plain.value = 7; Generic.value = 9;
            console.log(Base.stored, Plain.stored, Generic.stored,
                Object.hasOwn(Plain, "stored"), Object.hasOwn(Generic, "stored"));
            const alias: any = Plain;
            alias["value"] = 11;
            console.log(Base.stored, Plain.stored, Generic.stored, Object.hasOwn(Plain, "value"));
            """, "0 7 9 true true\n0 11 9 false\n"];
        yield return ["receiver-rhs-order-and-pending-rhs", """
            const state: any = { trace: "", selected: null, owner: null };
            class Base {
                static stored = 0;
                static set value(next: number) { state.trace += "s"; state.owner = this; this.stored = next; }
            }
            class First extends Base {}
            class Other extends Base {}
            function receiver(): any { state.trace += "r"; return state.selected; }
            function rhs(): number { state.trace += "v"; state.selected = Other; return 7; }
            state.selected = First;
            const assigned = receiver().value = rhs();
            console.log(state.trace, assigned, state.owner === First, Base.stored, First.stored, Other.stored);
            async function assign(pending: Promise<number>): Promise<any> { return receiver().value = await pending; }
            async function run() {
                state.trace = ""; state.selected = First;
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                const task = assign(pending);
                console.log("pending", state.trace);
                state.selected = Other; release(9);
                const result: any = await task;
                console.log(state.trace, result, state.owner === First, Base.stored, First.stored, Other.stored);
            }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """, "rvs 7 true 0 7 0\npending r\nrs 9 true 0 9 0\n"];
        yield return ["live-descriptors-and-nearer-getter-only", """
            const state: any = { owner: null, writes: 0, gets: 0 };
            class Base {
                static stored = 0;
                static set value(next: number) { this.stored = next; }
            }
            class Derived extends Base {}
            Object.defineProperty(Base, "value", {
                set: function(next: number) { state.writes++; state.owner = this; (this as any).stored = next * 2; },
                configurable: true
            });
            const assigned = Derived.value = 4;
            console.log(Base.stored, Derived.stored, assigned, state.owner === Derived, state.writes,
                Object.hasOwn(Derived, "value"));
            Object.defineProperty(Derived, "value", {
                get: function() { state.gets++; return 99; }, configurable: true
            });
            function strictWrite() { "use strict"; Derived.value = 5; }
            try { strictWrite(); }
            catch (error) { console.log(error instanceof TypeError, state.writes, state.gets, Base.stored, Derived.stored); }
            delete (Derived as any).value;
            Derived.value = 6;
            console.log(Base.stored, Derived.stored, state.writes, state.gets, state.owner === Derived);
            Object.defineProperty(Base, "value", { value: 10, writable: true, enumerable: true, configurable: true });
            Derived.value = 9;
            console.log((Base as any).value, (Derived as any).value, Object.hasOwn(Derived, "value"),
                state.writes, Base.stored, Derived.stored);
            """, "0 8 4 true 1 false\ntrue 1 0 0 8\n0 12 2 0 true\n10 9 true 2 0 12\n"];
        yield return ["abstract-computed-and-static-initialization", """
            abstract class Base {
                static stored = 0;
                static set value(next: number) { (this as any).stored = next; }
            }
            class Derived extends Base {
                static first = Derived.value = 7;
                static { this.value = 9; }
            }
            console.log(Base.stored, Derived.stored, Derived.first, Object.hasOwn(Derived, "stored"));
            class C {
                static stored = 0;
                static set ["value"](next: number) { this.stored = next; }
                static first = C.value = 13;
            }
            const setter: any = Object.getOwnPropertyDescriptor(C, "value")!.set;
            console.log(C.stored, C.first, setter.name, setter.length,
                setter === Object.getOwnPropertyDescriptor(C, "value")!.set);
            """, "0 9 7 true\n13 13 set value 1 true\n"];
        yield return ["mixed-auto-accessor-control", """
            const key: any = "value";
            class C {
                static accessor label: string = "one";
                static set [key](value: number) {}
            }
            C.label = "three";
            console.log(C.label);
            """, "three\n"];
        yield return ["generic-sloppy-descriptor-policy", """
            const state = { gets: 0, sets: 0 };
            class Base<T> {
                static stored = 0;
                static set value(next: number) { state.sets++; this.stored = next; }
            }
            class Derived extends Base<number> {}
            function sloppy(next: number) { return Derived.value = next; }
            Object.defineProperty(Base, "value", {
                get: function() { state.gets++; return 10; }, set: undefined, configurable: true
            });
            console.log(sloppy(7), state.gets, state.sets, Object.hasOwn(Derived, "value"), Base.stored);
            Object.defineProperty(Base, "value", { value: 11, writable: false, configurable: true });
            console.log(sloppy(8), (Base as any).value, Object.hasOwn(Derived, "value"), state.gets, state.sets);
            Object.defineProperty(Base, "value", { value: 13, writable: true, configurable: true });
            const assigned = sloppy(9);
            const own: any = Object.getOwnPropertyDescriptor(Derived, "value");
            console.log(assigned, (Base as any).value, (Derived as any).value,
                Object.hasOwn(Derived, "value"), state.gets, state.sets);
            console.log(own.writable, own.enumerable, own.configurable, Base.stored);
            """, "7 0 0 false 0\n8 11 false 0 0\n9 13 9 true 0 0\ntrue true true 0\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void SourceStaticSettersPreserveTheirActualConstructorReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
