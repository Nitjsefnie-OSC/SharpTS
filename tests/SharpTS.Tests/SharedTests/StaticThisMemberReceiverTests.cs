using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticThisMemberReceiverTests
{
    private const string Method = """
        class Base {
            protected static value(): string { return 'base'; }
            static read(): string { return this.value(); }
        }
        class Child extends Base { protected static value(): string { return 'child'; } }
        console.log(Child.read());
        """;

    private const string Field = """
        class Base { static tag = 1; static value(): number { return this.tag; } }
        class Child extends Base { static tag = 2; static value(): number { return super.value(); } }
        class Leaf extends Child { static tag = 3; }
        console.log(Child.value(), Leaf.value());
        """;

    private const string MethodValue = """
        class Base {
            static value(): string { return 'base'; }
            static read(): string { const value = this.value; return value.call(this); }
        }
        class Child extends Base { static value(): string { return 'child'; } }
        console.log(Child.read());
        """;

    private const string StaticInitialization = """
        class Base {
            static tag = 'base';
            static value(): string { return this.tag; }
            static { console.log('init', this.value()); }
        }
        class Child extends Base { static tag = 'child'; }
        console.log(Child.value());
        """;

    private const string AnyMethodControl = """
        class Base {
            protected static value(): string { return 'base'; }
            static read(): string { const receiver: any = this; return receiver.value(); }
        }
        class Child extends Base { protected static value(): string { return 'child'; } }
        console.log(Child.read());
        """;

    private const string AnyFieldControl = """
        class Base { static tag = 1; static value(): number { const receiver: any = this; return receiver.tag; } }
        class Child extends Base { static tag = 2; static value(): number { return super.value(); } }
        class Leaf extends Child { static tag = 3; }
        console.log(Child.value(), Leaf.value());
        """;

    private const string EvaluationOrder = """
        let trace = '';
        function argument(): any {
            trace += 'arg,';
            Object.defineProperty(Child, 'pack', { value: () => false, configurable: true });
            return undefined;
        }
        function spread(): any[] { trace += 'spread,'; return [8, 9]; }
        class Base {
            static pack(first: any = 'fallback', ...tail: any[]): any {
                trace += 'body,';
                console.log('pack', this === Child, first, tail.length, tail[0], tail[1], arguments.length);
                return this;
            }
            static run(): any { return this.pack(argument(), ...spread()); }
        }
        class Child extends Base {}
        const original: any = Child.pack;
        Object.defineProperty(Child, 'pack', {
            get: function(this: any) { trace += 'get,'; return original; }, configurable: true
        });
        console.log('result', Child.run() === Child, trace);
        """;

    private const string ThrowAndGetter = """
        class Base {
            static tag = 'base';
            static fail(): void { console.log('throw', this.tag); throw new Error('boom'); }
            static run(): string {
                const before = this;
                try { this.fail(); }
                catch (error: any) { console.log('caught', error.message, this === before); }
                return this.tag;
            }
            static get label(): string { return this.tag; }
            static readGetter(): string { return this.label; }
        }
        class Child extends Base { static tag = 'child'; }
        console.log(Child.run(), Child.readGetter());
        """;

    private const string MethodIdentity = """
        class Base {
            static tag = 'base';
            static value(): string { return this.tag; }
            static inspect(): void {
                const value = this.value;
                console.log(value === this.value, value.call(this));
            }
        }
        class Child extends Base { static tag = 'child'; }
        class Leaf extends Child { static tag = 'leaf'; }
        Child.inspect(); Leaf.inspect();
        """;

    private const string PendingArgument = """
        class Base {
            static tag = 'base';
            static pack(value: any): any { console.log(this.tag, value); return this; }
            static async run(gate: any): Promise<any> { return this.pack(await gate); }
        }
        class Child extends Base { static tag = 'child'; }
        async function main(): Promise<void> {
            let release: any;
            const gate = new Promise<any>(resolve => { release = resolve; });
            const pending = Child.run(gate);
            Object.defineProperty(Child, 'pack', {
                value: () => { console.log('replacement'); return false; }, configurable: true
            });
            release(7);
            console.log((await pending) === Child);
        }
        main();
        """;

    private const string YieldedArgument = """
        class Base {
            static tag = 'base';
            static pack(value: any): any { console.log(this.tag, value); return this; }
            static *run(): Generator<any, void, any> {
                const value = this.pack;
                console.log(this.pack(yield 'pause') === this);
                console.log(value.call(this, 9) === this);
            }
        }
        class Child extends Base { static tag = 'child'; }
        const iterator = Child.run();
        console.log(iterator.next().value);
        Object.defineProperty(Child, 'pack', {
            value: () => { console.log('replacement'); return false; }, configurable: true
        });
        console.log(iterator.next(7).done);
        """;

    private const string Optional = """
        let effects = 0;
        function argument(): number { effects++; return 1; }
        class Base {
            static tag = 'base';
            static target(value?: number): any { return this; }
            static field(): any { return this?.tag; }
            static optionalReceiver(): any { return this?.target(argument()); }
            static optionalMethod(): any { return this.target?.(argument()); }
        }
        class Child extends Base { static tag = 'child'; }
        const field: any = Base.field;
        const receiver: any = Base.optionalReceiver;
        const method: any = Base.optionalMethod;
        console.log(field.call(null) === undefined, field.call(undefined) === undefined,
            receiver.call(null) === undefined, receiver.call(undefined) === undefined, effects);
        console.log(field.call(Child), receiver.call(Child) === Child, effects);
        Object.defineProperty(Child, 'target', { value: undefined, configurable: true });
        console.log(method.call(Child) === undefined, effects);
        """;

    private const string GenericLibrary = """
        export class Base<T> {
            static tag = 'base';
            static value(): string { return this.tag; }
            static read(): string { return this.value(); }
        }
        export class Child<T> extends Base<T> {
            static tag = 'child';
            static value(): string { return this.tag + '!'; }
        }
        export class Leaf<T> extends Child<T> { static tag = 'leaf'; }
        """;

    private const string GenericEntry = """
        import { Child as Imported, Leaf } from './owner';
        console.log(Imported.read(), Leaf.read());
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return Case("method", Method, "child\n");
        yield return Case("field-super", Field, "2 3\n");
        yield return Case("method-value", MethodValue, "child\n");
        yield return Case("static-initialization", StaticInitialization, "init base\nchild\n");
        yield return Case("any-method-control", AnyMethodControl, "child\n");
        yield return Case("any-field-control", AnyFieldControl, "2 3\n");
        yield return Case("evaluation-order", EvaluationOrder, "pack true fallback 2 8 9 3\nresult true get,arg,spread,body,\n");
        yield return Case("throw-and-getter", ThrowAndGetter, "throw child\ncaught boom true\nchild child\n");
        yield return Case("method-identity", MethodIdentity, "true child\ntrue leaf\n");
        yield return Case("pending-argument", PendingArgument, "child 7\ntrue\n");
        yield return Case("yielded-argument", YieldedArgument, "pause\nchild 7\ntrue\nchild 9\ntrue\ntrue\n");
        yield return Case("optional", Optional, "true true true true 0\nchild true 1\ntrue 1\n");
        yield return Case("imported-generic", GenericEntry, "child! leaf!\n",
            new Dictionary<string, string> { ["owner.ts"] = GenericLibrary });
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void StaticThisMembersUseTheirActualConstructorReceiver(
        string name, Dictionary<string, string> files, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(files, "main.ts", mode));
    }

    private static object[] Case(string name, string source, string expected,
        Dictionary<string, string>? files = null)
    {
        files ??= new Dictionary<string, string>();
        files["main.ts"] = source;
        return [name, files, expected];
    }
}
