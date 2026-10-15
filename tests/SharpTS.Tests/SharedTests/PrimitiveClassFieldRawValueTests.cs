using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrimitiveClassFieldRawValueTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-and-required-constructor", """
            class C {
                value: number;
                flag: boolean;
                constructor(value: number = 9, flag: boolean = true) {
                    this.value = value;
                    this.flag = flag;
                    console.log(value, flag, this.value, this.flag);
                }
            }
            new C(null as any, null as any);
            new C(undefined, undefined);
            class RequiredFields {
                value: number;
                flag: boolean;
                constructor(value: number, flag: boolean) {
                    this.value = value; this.flag = flag;
                    console.log(value, flag, this.value, this.flag);
                }
            }
            new RequiredFields(null as any, null as any);
            """, "null null null null\n9 true 9 true\nnull null null null\n"];
        yield return ["transitions-read-routes-and-projections", """
            class C { value: number = 1; flag: boolean = true; }
            function show(value: C) {
                const alias: any = value;
                const descriptors: any = Object.getOwnPropertyDescriptors(value);
                const copy: any = Object.assign({}, value);
                console.log(value.value, value.flag, alias.value, alias.flag, alias["value"], alias["flag"],
                    Reflect.get(value, "value"), Reflect.get(value, "flag"),
                    descriptors.value.value, descriptors.flag.value, copy.value, copy.flag);
                const values: any = (Object as any).values(value), entries: any = (Object as any).entries(value);
                console.log(values[0], values[1], entries[0][1], entries[1][1]);
            }
            const value = new C();
            value.value = null as any; value.flag = null as any; show(value);
            value.value = 7; value.flag = false; show(value);
            const erased: any = value;
            erased.value = undefined; erased.flag = undefined; show(value);
            const alias: any = value;
            alias["value"] = "raw"; alias["flag"] = "flag"; show(value);
            """, "null null null null null null null null null null null null\nnull null null null\n7 false 7 false 7 false 7 false 7 false 7 false\n7 false 7 false\nundefined undefined undefined undefined undefined undefined undefined undefined undefined undefined undefined undefined\nundefined undefined undefined undefined\nraw flag raw flag raw flag raw flag raw flag raw flag\nraw flag raw flag\n"];
        yield return ["raw-object-identity-without-conversion", """
            const state = { hooks: 0 };
            const raw: any = {
                [Symbol.toPrimitive](hint: string) { state.hooks++; throw "converted"; },
                valueOf() { state.hooks++; throw "valueOf"; },
                toString() { state.hooks++; throw "toString"; }
            };
            class C {
                value: number; flag: boolean;
                constructor(value: any, flag: any) { this.value = value; this.flag = flag; }
            }
            const value = new C(raw, raw);
            const descriptors: any = Object.getOwnPropertyDescriptors(value);
            const copy: any = Object.assign({}, value);
            console.log(Object.is(value.value, raw), Object.is(value.flag, raw),
                Object.is(Reflect.get(value, "value"), raw), Object.is(descriptors.value.value, raw),
                Object.is(copy.flag, raw), state.hooks);
            value.value = 3; value.flag = false;
            const alias: any = value;
            const result: any = (alias.value = raw), flagResult: any = (alias.flag = raw);
            console.log(Object.is(result, raw), Object.is(flagResult, raw),
                Object.is(value.value, raw), Object.is(value.flag, raw), state.hooks);
            """, "true true true true true 0\ntrue true true true 0\n"];
        yield return ["generic-owner-inheritance-and-nearer-shadow", """
            class Base<T> {
                value: number; flag: boolean;
                constructor(value: number, flag: boolean) { this.value = value; this.flag = flag; }
            }
            class Middle<U> extends Base<U[]> {}
            class Leaf extends Middle<string> { own = 5; }
            const first = new Leaf(null as any, undefined as any), second = new Leaf(4, true);
            const view: Base<string[]> = first as any;
            view.value = undefined as any; first.flag = null as any;
            const descriptor: any = Object.getOwnPropertyDescriptor(first, "value");
            const copy: any = Object.assign({}, first);
            console.log(first.value, first.flag, view.value, descriptor.value, copy.value, second.value, second.flag);
            first.value = 8; first.flag = false;
            console.log(first.value, first.flag, second.value, second.flag);
            class Shadow extends Base<number> {
                value: any = "derived";
                constructor() { super(null as any, null as any); }
            }
            const shadow = new Shadow(), baseView: Base<number> = shadow;
            const shadowCopy: any = Object.assign({}, shadow);
            console.log(shadow.value, baseView.value, Reflect.get(shadow, "value"), shadowCopy.value, shadow.flag);
            baseView.value = null as any;
            console.log(shadow.value, baseView.value, Reflect.get(shadow, "value"));
            """, "undefined null undefined undefined undefined 4 true\n8 false 4 true\nderived derived derived derived null\nnull null null\n"];
        yield return ["initializers-readonly-and-parameter-properties", """
            const source: any = { value: null, flag: undefined };
            class Initialized {
                value: number = source.value;
                flag: boolean = source.flag;
                readonly saved: number;
                constructor(value: number) { this.saved = value; }
            }
            const value = new Initialized(null as any);
            const descriptor: any = Object.getOwnPropertyDescriptor(value, "saved");
            console.log(value.value, value.flag, value.saved, descriptor.value);
            class ParameterFields { constructor(public value: number, public flag: boolean) {} }
            const parameters = new ParameterFields(null as any, undefined as any);
            console.log(parameters.value, parameters.flag);
            """, "null undefined null null\nnull undefined\n"];
        yield return ["expressions-and-evaluated-declarations", """
            function expression(input: any): any {
                return class {
                    value: number = input; flag: boolean = input;
                    constructor() { console.log(input, this.value, this.flag); }
                };
            }
            function declaration(input: any): any {
                class Local {
                    value: number = input; flag: boolean = input;
                    constructor() { console.log(input, this.value, this.flag); }
                }
                return Local;
            }
            const A: any = expression(null), B: any = expression(undefined);
            const C: any = declaration(null), D: any = declaration(undefined);
            const a: any = new A(), b: any = new B(), c: any = new C(), d: any = new D();
            a.value = 7; c.flag = false;
            console.log(a.value, b.value, c.value, c.flag, d.value, d.flag);
            """, "null null null\nundefined undefined undefined\nnull null null\nundefined undefined undefined\n7 undefined null false undefined undefined\n"];
        yield return ["raw-returns-and-effectful-alias-invalidation", """
            class C {
                value: number; flag: boolean;
                constructor(value: number, flag: boolean) { this.value = value; this.flag = flag; }
                read(): number { return this.value; }
                readFlag(): boolean { return this.flag; }
            }
            function numberValue(value: C): number { return value.value; }
            function booleanValue(value: C): boolean { return value.flag; }
            function change(value: C) {
                const alias: any = value; alias.value = null; alias.flag = undefined;
            }
            const value = new C(null as any, null as any);
            console.log(value.read(), value.readFlag(), numberValue(value), booleanValue(value));
            value.value = 7; value.flag = true;
            change(value);
            console.log(value.value, value.flag, value.read(), value.readFlag(), numberValue(value), booleanValue(value));
            function conditional(value: C, take: boolean): number {
                if (take) { value.value = 7; }
                return value.value;
            }
            function skippedLoop(value: C, count: number): number {
                for (let index = 0; index < count; index++) { value.value = 8; }
                return value.value;
            }
            console.log(conditional(value, false), skippedLoop(value, 0));
            class SetterMutation {
                value: number = 1;
                set rewrite(value: number) { this.value = null as any; }
                step(): number { this.rewrite = 1; return this.value; }
            }
            console.log(new SetterMutation().step());
            let leaked: any;
            function save(receiver: any) { leaked = receiver; return 0; }
            class EscapedDefault {
                value: number = 1;
                constructor(unused = save(this)) {}
                step(): number { this.value = this.value + 1; return this.value; }
            }
            const escaped = new EscapedDefault();
            leaked.value = "raw";
            console.log(escaped.step());
            function ordinaryPlus(value: C): number { return value.value + 1; }
            const arrowPlus = (value: C): number => value.value + 1;
            const alias: any = value; alias.value = "raw";
            console.log(ordinaryPlus(value), arrowPlus(value));
            """, "null null null null\nnull undefined null undefined null undefined\nnull null\nnull\nraw1\nraw1 raw1\n"];
        yield return ["once-only-assignment-and-rejected-frozen-writes", """
            class C { value: number = 1; flag: boolean = true; }
            const value = new C(), state = { receivers: 0, values: 0 };
            function receiver(): C { state.receivers++; return value; }
            function right(): any { state.values++; return null; }
            const assigned: any = (receiver().value = right());
            console.log(state.receivers, state.values, assigned, value.value);
            const assignedFlag: any = (receiver().flag = right());
            console.log(state.receivers, state.values, assignedFlag, value.flag);
            Object.freeze(value);
            const alias: any = value;
            alias.value = 7;
            const rejected: any = (alias.flag = false);
            console.log(rejected, value.value, value.flag);
            function strictWrite() { "use strict"; alias.value = 9; }
            try { strictWrite(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(Reflect.set(value, "value", 8), Reflect.set(value, "flag", true), value.value, value.flag);
            """, "1 1 null null\n2 2 null null\nfalse null null\ntrue\nfalse false null null\n"];
    }

    public static IEnumerable<object[]> CompiledCases()
    {
        foreach (var row in Cases()) yield return row;
        // Successful Reflect.set on interpreter source-class fields is a separate boundary.
        yield return ["reflect-set-raw-and-native-transitions", """
            class C { value: number = 1; flag: boolean = true; }
            const value = new C(), other = new C();
            console.log(Reflect.set(value, "value", null), Reflect.set(value, "flag", undefined), value.value, value.flag);
            console.log(Reflect.set(value, "value", 7), Reflect.set(value, "flag", false), value.value, value.flag);
            const raw: any = {};
            const numberSet = Reflect.set(value, "value", raw), booleanSet = Reflect.set(value, "flag", raw);
            const descriptor: any = Object.getOwnPropertyDescriptor(value, "value");
            const copy: any = Object.assign({}, value);
            console.log(numberSet, booleanSet, Object.is(value.value, raw), Object.is(value.flag, raw),
                Object.is(descriptor.value, raw), Object.is(copy.flag, raw));
            console.log(other.value, other.flag);
            """, "true true null undefined\ntrue true 7 false\ntrue true true true true true\n1 true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Interpreted })
        .Concat(CompiledCases().Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Compiled }));

    [Theory, MemberData(nameof(ModeCases))]
    public void GuestPrimitiveFieldsPreserveAcceptedRawValues(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
