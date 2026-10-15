using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionAccessorFacetTests
{
    public const string IssueSource = """
        let stored: number = 9;
        const C = class {
            static get value(): number { return stored; }
            get value(): boolean { return true; }
        };
        console.log(C.value, new C().value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "9 true\n"];
        yield return ["reversed-declaration-order", """
            let stored: number = 9;
            const C = class {
                get value(): boolean { return true; }
                static get value(): number { return stored; }
            };
            console.log(C.value, new C().value);
            stored = 11;
            console.log(C.value, new C().value);
            """, "9 true\n11 true\n"];
        yield return ["getter-setter-pairs", """
            const state: any = {
                staticValue: 10, instanceValue: "initial", staticSets: 0, instanceSets: 0,
                staticOwner: null, instanceOwner: null
            };
            const C = class {
                constructor() {}
                static get value(): number { return state.staticValue; }
                get value(): string { return state.instanceValue; }
                static set value(next: number) {
                    state.staticValue = next; state.staticSets++; state.staticOwner = this;
                }
                set value(next: string) {
                    state.instanceValue = next; state.instanceSets++; state.instanceOwner = this;
                }
            };
            const instance = new C();
            const staticResult = C.value = 21;
            const instanceResult = instance.value = "changed";
            console.log(staticResult, instanceResult, C.value, instance.value,
                state.staticSets, state.instanceSets, state.staticOwner === C, state.instanceOwner === instance);
            C.value = 34;
            console.log(C.value, instance.value, state.staticSets, state.instanceSets);
            instance.value = "last";
            console.log(C.value, instance.value, state.staticSets, state.instanceSets);
            """, "21 changed 21 changed 1 1 true true\n34 changed 2 1\n34 last 2 2\n"];
        yield return ["raw-descriptor-receivers-and-identity", """
            const state = { staticPrefix: "static", instancePrefix: "instance" };
            const C = class Named {
                static get value(): any { return state.staticPrefix + ":" + (this as any).slot; }
                get value(): any { return state.instancePrefix + ":" + (this as any).slot; }
                static set value(next: number) { (this as any).slot = next * 10; }
                set value(next: number) { (this as any).slot = next + 1; }
            };
            const statics: any = Object.getOwnPropertyDescriptor(C, "value");
            const instance: any = Object.getOwnPropertyDescriptor(C.prototype, "value");
            console.log(statics.get !== instance.get, statics.set !== instance.set,
                statics.get === Object.getOwnPropertyDescriptor(C, "value")!.get,
                instance.set === Object.getOwnPropertyDescriptor(C.prototype, "value")!.set);
            const left: any = { slot: 2 }, right: any = { slot: 3 };
            console.log(statics.get.call(left), instance.get.call(right));
            statics.set.call(left, 4); instance.set.call(right, 4);
            console.log(left.slot, right.slot, statics.get.call(left), instance.get.call(right));
            state.staticPrefix = "S"; state.instancePrefix = "I";
            console.log(statics.get.call(left), instance.get.call(right));
            """, "true true true true\nstatic:2 instance:3\n40 5 static:40 instance:5\nS:40 I:5\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ClassExpressionAccessorsKeepTheirStaticAndInstanceFacets(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
