using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StrictGetterOnlyClassAssignmentTests
{
    public const string IssueSource = """
        class Base {
            stored = 0;
            get value(): number { return this.stored; }
            set value(v: number) { this.stored = v; }
        }
        class Derived extends Base { override get value(): number { return 9; } }
        const d: any = new Derived();
        d.value = 10;
        console.log(d.value, d["value"], d.stored);
        function strictWrite() { "use strict"; d.value = 11; }
        try { strictWrite(); } catch (error) { console.log(error instanceof TypeError); }
        console.log(d.stored);
        """;

    public const string FormsAndOrderSource = """
        const counts = { baseGets: 0, gets: 0, sets: 0 };
        const effects = { order: "", receiverThrow: false, keyThrow: false, valueThrow: false };
        const marker = {};
        let current: any;
        class Base {
            stored = 0;
            get value() { counts.baseGets++; return this.stored; }
            set value(next: number) { counts.sets++; this.stored = next; }
        }
        class Nearest extends Base { override get value() { counts.gets++; return 9; } }
        class Declared extends Nearest {}
        const Expression = class extends Base { get value() { counts.gets++; return 9; } };
        function receiver(): any {
            effects.order += "r";
            if (effects.receiverThrow) throw marker;
            return current;
        }
        function key(): string {
            effects.order += "k";
            if (effects.keyThrow) throw marker;
            return "value";
        }
        function rhs(): number {
            effects.order += "v";
            if (effects.valueThrow) throw marker;
            return 12;
        }
        function sloppyDot(target: any, value: number) { return target.value = value; }
        function sloppyIndex(target: any, name: string, value: number) { return target[name] = value; }
        function strictDot() { "use strict"; return receiver().value = rhs(); }
        function strictIndex() { "use strict"; return receiver()[key()] = rhs(); }
        for (const pair of [[Nearest, Declared], [Expression, Expression]] as any[]) {
            const Sloppy: any = pair[0], Strict: any = pair[1];
            current = new Sloppy();
            counts.baseGets = 0; counts.gets = 0; counts.sets = 0;
            console.log(sloppyDot(current, 10), sloppyIndex(current, "value", 11), current.stored,
                Object.hasOwn(current, "value"), counts.baseGets, counts.gets, counts.sets);
            current = new Strict();
            for (const write of [strictDot, strictIndex]) {
                effects.order = "";
                try { write(); console.log(false); }
                catch (error) { console.log(error instanceof TypeError, effects.order); }
            }
            console.log(current.stored, counts.baseGets, counts.gets, counts.sets);
            effects.order = ""; effects.valueThrow = true;
            try { strictIndex(); console.log(false); }
            catch (error) { console.log(error === marker, effects.order); }
            effects.valueThrow = false;
            effects.order = ""; effects.receiverThrow = true;
            try { strictIndex(); console.log(false); }
            catch (error) { console.log(error === marker, effects.order); }
            effects.receiverThrow = false;
            effects.order = ""; effects.keyThrow = true;
            try { strictIndex(); console.log(false); }
            catch (error) { console.log(error === marker, effects.order); }
            effects.keyThrow = false;
            console.log(current.value, counts.gets);
        }
        """;

    public const string OwnOverrideSource = """
        const state = { gets: 0, sets: 0, same: false };
        class Base { stored = 0; get value() { state.gets++; return 9; } }
        class Child extends Base {}
        const target: any = new Child();
        function strictDot(value: any, next: number) { "use strict"; return value.value = next; }
        function strictIndex(value: any, key: string, next: number) { "use strict"; return value[key] = next; }
        Object.defineProperty(target, "value", { value: 1, writable: true, configurable: true });
        console.log(strictDot(target, 2), target.value, Object.hasOwn(target, "value"), state.gets);
        const getter = function(this: any) { state.gets++; return this.stored + 10; };
        const setter = function(this: any, next: number) {
            state.sets++; state.same = this === target; this.stored = next;
        };
        Object.defineProperty(target, "value", { get: getter, set: setter, configurable: true });
        console.log(strictIndex(target, "value", 3), target.stored, state.sets, state.same, state.gets);
        const descriptor: any = Object.getOwnPropertyDescriptor(target, "value");
        console.log(descriptor.get === getter, descriptor.set === setter, Object.hasOwn(target, "value"));
        console.log(target.value, state.gets);
        """;

    public const string InheritedSetterSource = """
        const state: any = { gets: 0, sets: 0, same: true, expected: null };
        const marker = {};
        class Base {
            stored = 0;
            get value() { state.gets++; return this.stored; }
            set value(next: number) {
                state.sets++; state.same = state.same && this === state.expected;
                this.stored = next;
                if (next === 99) throw marker;
            }
        }
        class Declared extends Base {}
        const Expression = class extends Base {};
        function strictDot(target: any, next: number) { "use strict"; return target.value = next; }
        function strictIndex(target: any, key: string, next: number) { "use strict"; return target[key] = next; }
        for (const Constructor of [Declared, Expression] as any[]) {
            const target: any = new Constructor();
            state.gets = 0; state.sets = 0; state.same = true; state.expected = target;
            const first = target.value = 7;
            const second = strictDot(target, 8);
            try { strictIndex(target, "value", 99); console.log(false); }
            catch (error) { console.log(error === marker); }
            console.log(first, second, target.stored, state.sets, state.same, state.gets, Object.hasOwn(target, "value"));
        }
        """;

    public const string ClassMethodSource = """
        const state = { gets: 0 };
        class Base { get value() { state.gets++; return 9; } }
        class Child extends Base {}
        class Writer {
            dot(target: any) { return target.value = 1; }
            index(target: any, key: string) { return target[key] = 2; }
        }
        const target: any = new Child(), writer = new Writer();
        try { writer.dot(target); console.log(false); }
        catch (error) { console.log(error instanceof TypeError); }
        try { writer.index(target, "value"); console.log(false); }
        catch (error) { console.log(error instanceof TypeError); }
        console.log(state.gets, Object.hasOwn(target, "value"));
        """;

    public const string ReflectSelectedSource = ClassMethodSource + "\nconsole.log(typeof Reflect.set);";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "9 9 0\ntrue\n0\n"];
        var forms = "10 11 0 false 0 0 0\ntrue rv\ntrue rkv\n0 0 0 0\ntrue rkv\ntrue r\ntrue rk\n9 1\n";
        yield return ["declaration-expression-order-and-masking", FormsAndOrderSource, forms + forms];
        yield return ["own-data-and-accessor-overrides", OwnOverrideSource,
            "2 2 true 0\n3 3 1 true 0\ntrue true true\n13 1\n"];
        var setter = "true\n7 8 99 3 true 0 false\n";
        yield return ["inherited-setter-receiver-and-abrupt-value", InheritedSetterSource, setter + setter];
        yield return ["class-method-strict-without-reflect", ClassMethodSource, "true\ntrue\n0 false\n"];
        yield return ["class-method-strict-reflect-selected", ReflectSelectedSource, "true\ntrue\n0 false\nfunction\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void StrictGetterOnlyWritesRejectWithoutChangingSloppyOrSetterSemantics(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
