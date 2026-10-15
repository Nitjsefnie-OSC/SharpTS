using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DerivedFieldInitializationOrderTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class Base {
                constructor() { console.log('base', (this as any).value); }
            }
            class Derived extends Base {
                value = (console.log('field'), 43);
                constructor() { super(); console.log('body', this.value); }
            }
            new Derived();
            """, "base undefined\nfield\nbody 43\n"];
        yield return ["explicit-and-implicit-levels", """
            const state: any = { trace: [], target: null };
            class Base {
                value = (state.trace.push("base.field"), 1);
                constructor() {
                    state.target = new.target;
                    state.trace.push("base.body:" + this.value + ":" + Object.hasOwn(this, "leaf"));
                }
            }
            class Middle extends Base { value = (state.trace.push("middle.field"), 2); }
            class Leaf extends Middle {
                leaf = (state.trace.push("leaf.field"), 3);
                constructor() { super(); state.trace.push("leaf.body:" + this.value); }
            }
            for (let index = 0; index < 2; index++) {
                state.trace = [];
                const value = new Leaf();
                console.log(state.trace.join("|"), value.value, value.leaf, state.target === Leaf);
            }
            class ImplicitLeaf extends Middle { leaf = (state.trace.push("implicit.field"), 4); }
            state.trace = [];
            const value = new ImplicitLeaf();
            console.log(state.trace.join("|"), value.value, value.leaf, state.target === ImplicitLeaf);
            """, "base.field|base.body:1:false|middle.field|leaf.field|leaf.body:2 2 3 true\nbase.field|base.body:1:false|middle.field|leaf.field|leaf.body:2 2 3 true\nbase.field|base.body:1:false|middle.field|implicit.field 2 4 true\n"];
        yield return ["branches-and-abrupt-parent", """
            const state: any = { trace: [] };
            const token = {};
            class Base {
                constructor(fail: boolean) { state.trace.push("base"); if (fail) throw token; }
            }
            class Derived extends Base {
                value = (state.trace.push("field"), 1);
                constructor(left: boolean, fail: boolean) {
                    if (left) { super(fail); } else { super(fail); }
                    state.trace.push("body");
                }
            }
            for (const left of [true, false]) {
                state.trace = []; new Derived(left, false); console.log(state.trace.join("|"));
            }
            state.trace = [];
            try { new Derived(true, true); }
            catch (error) { console.log(error === token, state.trace.join("|")); }
            class FailingField extends Base {
                first = (state.trace.push("first"), (() => { throw token; })());
                later = (state.trace.push("later"), 2);
                constructor() { super(false); state.trace.push("body"); }
            }
            state.trace = [];
            try { new FailingField(); }
            catch (error) { console.log(error === token, state.trace.join("|")); }
            """, "base|field|body\nbase|field|body\ntrue base\ntrue base|first\n"];
        yield return ["private-brand-and-auto-accessor-boundary", """
            const state: any = { trace: [] };
            class Base {
                constructor(probe: (value: any) => boolean) { state.trace.push("base:" + probe(this)); }
            }
            class Derived extends Base {
                first = (state.trace.push("first:" + (#method in this) + ":" + (#later in this)), 1);
                #later = (state.trace.push("private"), 2);
                last = (state.trace.push("last"), 3);
                accessor label: string = (state.trace.push("auto"), "label");
                #method() { return this.#later; }
                constructor() {
                    super((value: any) => #later in value);
                    state.trace.push("body:" + this.#method() + ":" + this.label);
                }
            }
            const value = new Derived();
            console.log(state.trace.join("|"), value.first, value.last, value.label);
            """, "base:false|first:true:false|private|last|auto|body:2:label 1 3 label\n"];
        yield return ["class-expression-levels", """
            const state: any = { trace: [] };
            const Base = class {
                value = (state.trace.push("base.field"), 1);
                constructor() { state.trace.push("base.body:" + this.value); }
            };
            const Middle = class extends Base { middle = (state.trace.push("middle.field"), 2); };
            const Leaf = class extends Middle {
                leaf = (state.trace.push("leaf.field"), 3);
                constructor() { super(); state.trace.push("leaf.body:" + this.middle); }
            };
            const value = new Leaf();
            console.log(state.trace.join("|"), value.value, value.middle, value.leaf);
            """, "base.field|base.body:1|middle.field|leaf.field|leaf.body:2 1 2 3\n"];
        yield return ["array-native-completion", """
            const state: any = { trace: [] };
            class Base extends Array<any> {
                field = (state.trace.push("base.field:" + this.length), 1);
                constructor(...values: any[]) {
                    state.trace.push("before"); super(...values); state.trace.push("base.body");
                }
            }
            class Middle extends Base { middle = (state.trace.push("middle.field:" + this.length), 2); }
            class Leaf extends Middle {
                leaf = (state.trace.push("leaf.field:" + this.length), 3);
                constructor(...values: any[]) { super(...values); state.trace.push("leaf.body"); }
            }
            const value = new Leaf("a", "b");
            console.log(state.trace.join("|"), Array.isArray(value), value.join(","), value.field, value.middle, value.leaf);
            state.trace = [];
            try { new Leaf(-1); }
            catch (error) { console.log(error instanceof RangeError, state.trace.join("|")); }
            """, "before|base.field:2|base.body|middle.field:2|leaf.field:2|leaf.body true a,b 1 2 3\ntrue before\n"];
        yield return ["error-native-completion", """
            const state: any = { trace: [] };
            const NativeError: any = Error;
            class Base extends NativeError {
                field = (state.trace.push("base.field:" + (this as any).message), 1);
                constructor(message: string) { super(message); state.trace.push("base.body"); }
            }
            class Middle extends Base { middle = (state.trace.push("middle.field"), 2); }
            class Leaf extends Middle {
                leaf = (state.trace.push("leaf.field"), 3);
                constructor(message: string) { super(message); state.trace.push("leaf.body"); }
            }
            const value: any = new Leaf("message");
            console.log(state.trace.join("|"), value.message, value.field, value.middle, value.leaf,
                value instanceof Error, value instanceof Leaf);
            """, "base.field:message|base.body|middle.field|leaf.field|leaf.body message 1 2 3 true true\n"];
        yield return ["promise-native-completion", """
            const state: any = { trace: [] };
            const NativePromise: any = Promise;
            class Base extends NativePromise {
                field = (state.trace.push("base.field"), 1);
                constructor(executor: any) {
                    state.trace.push("before"); super(executor); state.trace.push("base.body");
                }
            }
            class Middle extends Base { middle = (state.trace.push("middle.field"), 2); }
            class Leaf extends Middle {
                leaf = (state.trace.push("leaf.field"), 3);
                constructor(executor: any) { super(executor); state.trace.push("leaf.body"); }
            }
            const value: any = new Leaf((resolve: any) => { state.trace.push("executor"); resolve(7); });
            console.log(state.trace.join("|"), value instanceof Promise, value instanceof Leaf,
                value.field, value.middle, value.leaf);
            state.trace = [];
            try { new Leaf(7); }
            catch (error) { console.log("rejected", state.trace.join("|")); }
            const token = {};
            state.trace = [];
            const rejected: any = new Leaf(() => { state.trace.push("executor"); throw token; });
            console.log(state.trace.join("|"), rejected.field, rejected.middle, rejected.leaf);
            rejected.catch((error: any) => { console.log(error === token); });
            """, "before|executor|base.field|base.body|middle.field|leaf.field|leaf.body true true 1 2 3\nrejected before\nbefore|executor|base.field|base.body|middle.field|leaf.field|leaf.body 1 2 3\ntrue\n"];
        yield return ["caught-parent-throw-and-retry", """
            const state: any = { trace: [] };
            const token = {};
            class Base {
                field = (state.trace.push("base.field"), 1);
                constructor(fail: boolean) { state.trace.push("base:" + fail); if (fail) throw token; }
            }
            class Derived extends Base {
                #value = (state.trace.push("private"), 2);
                accessor label: string = (state.trace.push("auto"), "label");
                constructor() {
                    try { super(true); } catch (error) { state.trace.push("caught:" + (error === token)); }
                    super(false);
                    state.trace.push("body:" + this.#value + ":" + this.label);
                }
            }
            new Derived();
            console.log(state.trace.join("|"));
            """, "base.field|base:true|caught:true|base.field|base:false|private|auto|body:2:label\n"];
    }

    // The exact report retains the interpreter's required undefined observation.
    // The class-expression order control also passes verified saved compilation;
    // other controls retain interpreter coverage for separate compiler boundaries.
    public static IEnumerable<object[]> CompiledCases() => Cases().Where(row => (string)row[0] is
        "class-expression-levels");

    public static IEnumerable<object[]> ModeCases() => Cases()
        .Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Interpreted })
        .Concat(CompiledCases().Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Compiled }));

    [Theory, MemberData(nameof(ModeCases))]
    public void DerivedElementsInitializeAtTheirOwnSuccessfulSuperBoundary(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedJavaScriptConstructorContextSurvivesDefaultsAndLexicalArrows()
    {
        // Valid JavaScript in Node; TypeScript rejects super in parameter defaults
        // and nested arrows (TS2336/TS2337), so this is an explicit unchecked lane.
        const string source = """
            const state: any = { trace: [] };
            class Base {
                constructor(value: number) { state.trace.push("base:" + value); }
            }
            class DefaultSuper extends Base {
                field = (state.trace.push("default.field"), 1);
                constructor(value: any = super(7)) { state.trace.push("default.body:" + (value === this)); }
            }
            new DefaultSuper();
            console.log(state.trace.join("|"));
            state.trace = [];
            class Nested { constructor() { state.trace.push("nested"); } }
            class ArrowSuper extends Base {
                field = (state.trace.push("arrow.field"), 2);
                constructor() {
                    const call = () => super((new Nested(), 9));
                    call(); state.trace.push("arrow.body:" + this.field);
                }
            }
            new ArrowSuper();
            console.log(state.trace.join("|"));
            """;
        Assert.Equal("base:7|default.field|default.body:true\nnested|base:9|arrow.field|arrow.body:2\n",
            TestHarness.RunUnchecked(source, ExecutionMode.Interpreted));
    }

    [Fact]
    public void InterpretedFieldPublicationPreservesExistingOwnAccessorReads()
    {
        const string source = """
            class C {}
            const value: any = new C();
            Object.defineProperty(value, "x", {
                get: () => 1, set: (next: any) => {}, configurable: true
            });
            console.log(value.x);
            value["x"] = 2;
            console.log(value.x);
            """;
        Assert.Equal("1\n1\n", TestHarness.RunInterpreted(source));
    }
}
