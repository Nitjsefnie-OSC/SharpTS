using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class LiveSuperMethodCallTests
{
    public const string IssueSource = """
        class B { value() { return 1; } }
        class D extends B { read() { return super.value(); } }
        const prototype: any = B.prototype;
        prototype.value = () => 2;
        console.log(new D().read());
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", IssueSource, "2\n"];
        yield return ["replacement-receivers", """
            class B {
                tag = "base";
                value(suffix: string): any { return this.tag + suffix; }
            }
            class D extends B {
                tag = "child";
                read() { return super.value("!"); }
                grouped() { return (super.value)("?"); }
                asserted() { return (super.value as any)("."); }
            }
            class Leaf extends D { tag = "leaf"; }
            const child = new Leaf(), prototype: any = B.prototype;
            prototype.value = function(this: any, suffix: string) { return "one:" + this.tag + suffix; };
            (child as any).value = () => "own";
            console.log(child.read(), child.grouped(), child.asserted());
            prototype.value = function(this: any, suffix: string) { return "two:" + this.tag + suffix; };
            console.log(child.read());
            prototype.value = (function(this: any, suffix: string) { return this.tag + suffix; }).bind({ tag: "bound" });
            console.log(child.read());
            const owner = { tag: "lexical", make() { return () => this.tag; } };
            prototype.value = owner.make();
            console.log(child.read());
            """, "one:leaf! one:leaf? one:leaf.\ntwo:leaf!\nbound!\nlexical\n"];
        yield return ["unchanged-nonvirtual-and-arguments", """
            let order = "";
            function arg(value: number) { order += value; return value; }
            class A {
                tag = "base";
                value(prefix: string = "default", ...rest: number[]): any {
                    return [this.tag, prefix, rest.join(","), arguments.length].join("|");
                }
                first(value: number): any { return [this.tag, value, arguments.length].join("|"); }
                zero(): any { return "base:" + this.tag; }
            }
            class B extends A {
                tag = "child";
                value(prefix: string = "default", ...rest: number[]): any { return "override"; }
                zero(): any { return "override"; }
                read() {
                    return [super.value(), super.value(undefined, ...[1, 2]),
                        (super.value as any)("given", 3, 4, 5)].join(";");
                }
                extra() { return (super.first as any)(arg(1), arg(2)); }
                readZero() { return super.zero(); }
            }
            class Leaf extends B { tag = "leaf"; zero(): any { return "leaf override"; } }
            const child = new Leaf();
            console.log(child.readZero(), child.read());
            console.log(child.extra(), order);
            """, "base:leaf leaf|default||0;leaf|default|1,2|3;leaf|given|3,4,5|4\nleaf|1|2 12\n"];
        yield return ["descriptor-and-error-order", """
            let trace = "", fail = false;
            const token: any = {};
            function arg() { trace += "a"; if (fail) throw token; return 7; }
            class B { value(value: any): any { return value; } }
            class D extends B { tag = "child"; read() { return super.value(arg()); } }
            const child = new D(), prototype: any = B.prototype;
            let getterThis: any;
            Object.defineProperty(prototype, "value", {
                configurable: true,
                get: function(this: any): any {
                    trace += "g"; getterThis = this;
                    return function(this: any, value: any) { trace += "f"; return this.tag + ":" + value; };
                }
            });
            console.log(child.read(), trace, getterThis === child);
            trace = "";
            Object.defineProperty(prototype, "value", {
                configurable: true, get() { trace += "g"; throw token; }
            });
            try { child.read(); } catch (error) { console.log(error === token, trace); }
            trace = "";
            Object.defineProperty(prototype, "value", {
                configurable: true, get(): any { trace += "g"; return 0; }
            });
            try { child.read(); } catch (error) { console.log(error instanceof TypeError, trace); }
            trace = ""; fail = true;
            Object.defineProperty(prototype, "value", {
                configurable: true, get(): any {
                    trace += "g";
                    return function(value: any) { trace += "f"; return value; };
                }
            });
            try { child.read(); } catch (error) { console.log(error === token, trace); }
            """, "child:7 gaf true\ntrue g\ntrue ga\ntrue ga\n"];
        yield return ["shadowing-deletion-and-optional", """
            let calls = 0;
            function arg() { calls++; return 1; }
            class Root { value(value: any): any { return "root:" + value; } }
            class Base extends Root { value(value: any): any { return "base:" + value; } }
            class Child extends Base {
                read() { return super.value(arg()); }
                optional() { return super.value?.(arg()); }
                wrapped() { return (super.value as any)?.(arg()); }
            }
            const child = new Child(), prototype: any = Base.prototype, root: any = Root.prototype;
            console.log(child.read(), calls);
            delete prototype.value;
            console.log(child.read(), calls);
            for (const value of [undefined, null] as any[]) {
                Object.defineProperty(prototype, "value", { value, writable: true, configurable: true });
                console.log(child.optional() === undefined, child.wrapped() === undefined, calls);
            }
            Object.defineProperty(prototype, "value", { set(value: any) {}, configurable: true });
            console.log(child.optional() === undefined, child.wrapped() === undefined, calls);
            Object.defineProperty(prototype, "value", { value: 42, configurable: true });
            try { child.optional(); } catch (error) { console.log(error instanceof TypeError, calls); }
            delete prototype.value;
            console.log(child.optional(), calls);
            delete root.value;
            console.log(child.optional() === undefined, calls);
            try { child.read(); } catch (error) { console.log(error instanceof TypeError, calls); }
            """, "base:1 1\nroot:1 2\ntrue true 2\ntrue true 2\ntrue true 2\ntrue 3\nroot:1 4\ntrue 4\ntrue 5\n"];
        yield return ["lexical-arrows-static-and-async-expression", """
            class A {
                static tag = "base-static";
                static value(): any { return this.tag; }
                value(): any { return "base"; }
            }
            class B extends A {
                tag = "child";
                static tag = "child-static";
                make() { return () => () => super.value(); }
                static make() { return () => super.value(); }
                static read() { return super.value(); }
            }
            class Leaf extends B { tag = "leaf"; static tag = "leaf-static"; }
            const child = new Leaf(), read = child.make()(), staticRead = Leaf.make();
            const prototype: any = A.prototype, constructor: any = A;
            prototype.value = function(this: any) { return "instance:" + this.tag; };
            constructor.value = function(this: any) { return "static:" + this.tag; };
            console.log(read.call({ tag: "fake" }), staticRead.call({ tag: "fake" }), Leaf.read());
            let getterThis: any, staticGetterThis: any;
            Object.defineProperty(prototype, "value", {
                configurable: true, get: function(this: any): any {
                    getterThis = this;
                    return function(this: any) { return "getter:" + this.tag; };
                }
            });
            Object.defineProperty(constructor, "value", {
                configurable: true, get: function(this: any): any {
                    staticGetterThis = this;
                    return function(this: any) { return "static-getter:" + this.tag; };
                }
            });
            const instanceValue = read(), staticValue = staticRead();
            console.log(instanceValue, staticValue, getterThis === child, staticGetterThis === Leaf);
            Object.defineProperty(prototype, "value", {
                configurable: true, writable: true,
                value: async function(this: any) {
                    // Global Promise capture after prototype observation is #2196.
                    await 0;
                    return "async:" + this.tag;
                }
            });
            read().then((value: any) => console.log(value));
            """, "instance:leaf static:leaf-static static:leaf-static\ngetter:leaf static-getter:leaf-static true true\nasync:leaf\n"];
        yield return ["callee-before-suspended-argument", """
            class A { value(value: number): any { return "base:" + value; } }
            class B extends A {
                tag = "child";
                *generate(): Generator<any, any, any> { return super.value((yield "arg") as any); }
                async awaited(pending: Promise<number>) { return super.value(await pending); }
                arrow(pending: Promise<number>) { return async () => super.value(await pending); }
                async *asyncGenerate(): AsyncGenerator<any, any, any> { return super.value((yield "arg") as any); }
                *optional(): Generator<any, any, any> { return super.value?.((yield "unused") as any); }
            }
            const prototype: any = A.prototype;
            function replace(label: string) {
                prototype.value = function(this: any, value: number) { return label + ":" + this.tag + ":" + value; };
            }
            replace("first");
            const child = new B(), iterator = child.generate();
            console.log(iterator.next().value);
            child.tag = "changed"; replace("second");
            console.log(iterator.next(2).value);
            const again = new B().generate(); again.next();
            console.log(again.next(3).value);
            prototype.value = undefined;
            const skipped = new B().optional().next();
            console.log(skipped.done, skipped.value === undefined);
            async function runAsync() {
                const child = new B();
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                replace("await-first");
                const task = child.awaited(pending);
                child.tag = "changed"; replace("await-second"); release(4);
                console.log(await task);
                console.log(await child.awaited(Promise.resolve(5)));
            }
            async function runArrow() {
                const child = new B();
                let release: (value: number) => void = () => {};
                const pending = new Promise<number>(resolve => { release = resolve; });
                replace("arrow-first");
                const operation = child.arrow(pending), task = operation();
                replace("arrow-second"); release(6);
                console.log(await task);
                const again = child.arrow(Promise.resolve(7));
                console.log(await again());
            }
            async function runGenerator() {
                replace("generator-first");
                const iterator = new B().asyncGenerate();
                console.log((await iterator.next()).value);
                replace("generator-second");
                console.log((await iterator.next(8)).value);
            }
            async function run() { await runAsync(); await runArrow(); await runGenerator(); }
            run();
            """, "arg\nfirst:changed:2\nsecond:child:3\ntrue true\nawait-first:changed:4\nawait-second:changed:5\narrow-first:child:6\narrow-second:child:7\narg\ngenerator-first:child:8\n"];
        yield return ["generic-and-expression-owners", """
            class Base<T> { tag = "generic"; value(value: T): any { return value; } }
            class Child<T> extends Base<T> { read(value: T) { return super.value(value); } }
            const genericPrototype: any = Base.prototype;
            genericPrototype.value = function(this: any, value: any) { return this.tag + ":" + value; };
            console.log(new Child<string>().read("s"), new Child<number>().read(4));
            class Left { value(value: number): any { return "left:" + value; } }
            class Right { value(value: number): any { return "right:" + value; } }
            function make(parent: any) {
                return class Named extends parent {
                    tag = "expression";
                    constructor() { super(); }
                    read(value: number) { return super.value(value); }
                    arrow(value: number) { return () => super.value(value); }
                };
            }
            const A = make(Left), B = make(Right), first = new A(), second = new B();
            const readFirst = first.arrow(3), readSecond = second.arrow(4);
            const left: any = Left.prototype, right: any = Right.prototype;
            left.value = function(this: any, value: number) { return "L:" + this.tag + ":" + value; };
            right.value = function(this: any, value: number) { return "R:" + this.tag + ":" + value; };
            console.log(first.read(1), second.read(2));
            left.value = function(this: any, value: number) { return "LL:" + this.tag + ":" + value; };
            console.log(readFirst(), readSecond());
            """, "generic:s generic:4\nL:expression:1 R:expression:2\nLL:expression:3 R:expression:4\n"];
    }

    public const string AccessorBarrierSource = """
        class G { value() { return 1; } }
        class B extends G { get value() { return () => 2; } }
        class D extends B { read() { return super.value(); } }
        console.log(new D().read());
        class Setter extends G { set value(value) {} }
        class Leaf extends Setter { read() { return super.value(); } }
        try { new Leaf().read(); }
        catch (error) { console.log(error instanceof TypeError); }
        """;
    public const string AccessorBarrierExpected = "2\ntrue\n";

    public const string ProxySource = """
        let order = "";
        function arg() { order += "a"; return 7; }
        class A { value(value: any): any { return value; } }
        class B extends A { tag = "child"; read() { return super.value(arg()); } }
        const child = new B(), prototype: any = A.prototype;
        const target = function(this: any, value: any) { order += "f"; return this.tag + ":" + value; };
        prototype.value = new Proxy(target, {
            apply(target: any, receiver: any, args: any[]) {
                order += "p";
                console.log(receiver === child, args[0]);
                return target.apply(receiver, args);
            }
        });
        console.log(child.read(), order);
        """;
    public const string ProxyExpected = "true 7\nchild:7 apf\n";

    public const string NonCallableProxySource = """
        let order = "";
        function arg() { order += "a"; return 7; }
        class A { value(value: any): any { return value; } }
        class B extends A { read() { return super.value(arg()); } }
        const prototype: any = A.prototype;
        prototype.value = new Proxy({}, { apply() { order += "trap"; return 0; } });
        try { new B().read(); } catch (error) { console.log(error instanceof TypeError, order); }
        """;

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void SuperCallsReadLivePropertiesAndPreserveTheirReference(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ProxyReplacementPreservesReceiverAndCallability(ExecutionMode mode)
    {
        // Proxy has an existing soft runtime dependency (#2178), so this control
        // uses the normal hosted harness rather than the pure saved Cases.
        Assert.Equal(ProxyExpected, TestHarness.Run(ProxySource, mode));
    }

    [Theory, ModeData]
    public void DeclaredAccessorsStopNominalMethodSearch(ExecutionMode mode)
    {
        // Valid JavaScript overrides a method with an accessor. TypeScript's
        // TS2423 restriction is separate from the runtime's nominal-call proof.
        Assert.Equal(AccessorBarrierExpected, TestHarness.RunUnchecked(AccessorBarrierSource, mode));
    }

    [Fact]
    public void NonCallableProxySuperTargetRejectsAfterArgumentsInInterpreter()
    {
        // The interpreter's super-reference adapter rejects this target. The
        // general compiled Proxy callability defect is tracked separately (#2264).
        Assert.Equal("true a\n", TestHarness.Run(NonCallableProxySource, ExecutionMode.Interpreted));
    }
}
