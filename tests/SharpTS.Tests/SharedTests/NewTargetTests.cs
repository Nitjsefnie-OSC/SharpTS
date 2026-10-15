using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NewTargetTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["bound", """
            const targets: any[] = [];
            function A() { targets.push(new.target); }
            const Bound: any = (A as any).bind(), Nested: any = Bound.bind();
            new Bound();
            new Nested();
            A();
            A.call({});
            A.apply({}, []);
            console.log(targets[0] === A, targets[1] === A, targets[2] === undefined,
                targets[3] === undefined, targets[4] === undefined);
            """, "true true true true true\n"];
        yield return ["reflect", """
            let latest: any;
            function Target() { latest = new.target; }
            function Other() {}
            const Bound: any = (Target as any).bind();
            Reflect.construct(Bound, [], Bound);
            console.log(latest === Target);
            Reflect.construct(Target, [], Bound);
            console.log(latest === Bound);
            Reflect.construct(Bound, [], Other);
            console.log(latest === Other);
            Reflect.construct(Target, []);
            console.log(latest === Target);
            let rejected = 0;
            for (const invalid of [undefined, null, {}, () => {}] as any[]) {
                try { Reflect.construct(Target, [], invalid); }
                catch (error) { if (error instanceof TypeError) rejected++; }
            }
            console.log(rejected);
            """, "true\ntrue\ntrue\ntrue\n4\n"];
        yield return ["parameters", """
            function Target(value: any = new.target, ...rest: any[]) {
                console.log(value === new.target, arguments.length, Target.length, rest.length);
            }
            const Constructor: any = Target, Bound: any = Target.bind(null, 1);
            new Constructor();
            Target();
            new Bound(2);
            """, "true 0 0 0\ntrue 0 0 0\nfalse 2 0 1\n"];
        yield return ["arrows", """
            function Capture() { return () => new.target; }
            function Other() {}
            const Constructor: any = Capture;
            const ordinary = Capture(), constructed: any = new Constructor();
            const redirected: any = Reflect.construct(Capture, [], Other);
            Capture();
            console.log(ordinary() === undefined, constructed() === Capture, redirected() === Other);
            function AsyncCapture() {
                return async () => { await Promise.resolve(0); return new.target; };
            }
            function DefaultCapture(value: any = () => new.target) { return value; }
            const DefaultConstructor: any = DefaultCapture, defaultArrow: any = new DefaultConstructor();
            console.log(defaultArrow() === DefaultCapture, DefaultCapture()() === undefined);
            const AsyncConstructor: any = AsyncCapture, arrow: any = new AsyncConstructor();
            async function run() { console.log(await arrow() === AsyncCapture); }
            run();
            """, "true true true\ntrue true\ntrue\n"];
        yield return ["classes", """
            class Base {
                target: any;
                constructor() { this.target = new.target; }
            }
            class Derived extends Base {}
            console.log(new Base().target === Base, new Derived().target === Derived);
            class Explicit extends Base {
                constructor() {
                    console.log(new.target === Explicit);
                    super();
                    console.log(this.target === Explicit);
                }
            }
            new Explicit();
            const Bound: any = Derived.bind(null);
            console.log(new Bound().target === Derived);
            """, "true true\ntrue\ntrue\ntrue\n"];
        yield return ["expressions", """
            function make() {
                return class Named {
                    target: any;
                    constructor() { this.target = new.target; }
                };
            }
            const A = make(), B = make();
            console.log(new A().target === A, new B().target === B);
            class Derived extends A {}
            console.log(new Derived().target === Derived);
            """, "true true\ntrue\n"];
        yield return ["proxy", """
            function Target() { return { target: new.target }; }
            let trapped: any;
            const ProxyConstructor: any = new Proxy(Target, {
                construct(target: any, args: any[], newTarget: any) {
                    trapped = newTarget;
                    return Reflect.construct(target, args, newTarget);
                }
            });
            const result: any = new ProxyConstructor();
            console.log(trapped === ProxyConstructor, result.target === ProxyConstructor);
            const Plain: any = new Proxy(Target, {}), plain: any = new Plain();
            console.log(plain.target === Plain);
            """, "true true\ntrue\n"];
        yield return ["boundaries", """
            function* generator() { yield new.target; }
            const Named: any = function Inner() {
                const captured = () => new.target;
                function ordinary() { return new.target; }
                const expression = function () { return new.target; };
                console.log(new.target === Inner, ordinary() === undefined,
                    expression() === undefined, generator().next().value === undefined);
                return captured;
            };
            const snapshot: any = new Named();
            Named();
            console.log(snapshot() === Named);
            """, "true true true true\nfalse true true true\ntrue\n"];
        yield return ["generics", """
            let actualTarget: any;
            function outer<T>(value: T) {
                return function Local() { actualTarget = new.target; return { value }; };
            }
            const Constructor: any = outer<number>(3), result: any = new Constructor();
            console.log(result.value, actualTarget === Constructor);
            class Box<T> {
                value: T;
                target: any;
                constructor(value: T) { this.value = value; this.target = new.target; }
            }
            const box = new Box<number>(7);
            console.log(box.value, box.target === Box);
            """, "3 true\n7 true\n"];
        yield return ["computed", """
            function Outer(): any {
                class Decl {
                    [new.target === Outer ? 'constructed' : 'called']() { return 11; }
                    static [new.target === Outer ? 'constructed' : 'called']() { return 12; }
                }
                const Expr = class {
                    [new.target === Outer ? 'constructed' : 'called']() { return 21; }
                    static [new.target === Outer ? 'constructed' : 'called']() { return 22; }
                };
                return [Decl, Expr];
            }
            const Constructor: any = Outer, constructed: any = new Constructor();
            console.log(new constructed[0]().constructed(), constructed[0].constructed(),
                new constructed[1]().constructed(), constructed[1].constructed());
            const called = Outer();
            console.log(new called[0]().called(), called[0].called(),
                new called[1]().called(), called[1].called());
            """, "11 12 21 22\n11 12 21 22\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ConstructionContextAndLexicalArrowsObserveTheActualNewTarget(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("new.target;")]
    [InlineData("const arrow = () => new.target;")]
    [InlineData("function f() { return new.other; }")]
    [InlineData("function f() { new.target = f; }")]
    public void InvalidMetaPropertyContextsRemainParseErrors(string source)
    {
        var parser = new SharpTS.Parsing.Parser(new SharpTS.Parsing.Lexer(source).ScanTokens());
        Assert.False(parser.Parse().IsSuccess);
    }
}
