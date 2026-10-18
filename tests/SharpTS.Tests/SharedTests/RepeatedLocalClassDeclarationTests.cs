using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class RepeatedLocalClassDeclarationTests
{
    public const string OriginalSource = """
        function make() { class C {} return C; }
        const First: any = make(), Second: any = make();
        const first: any = new First(), second: any = new Second();
        console.log(First === Second);
        console.log(First.prototype === Second.prototype);
        console.log(first instanceof Second, second instanceof First);
        function makeExpression() { return class C {}; }
        const ExpressionFirst: any = makeExpression(), ExpressionSecond: any = makeExpression();
        console.log(ExpressionFirst === ExpressionSecond, ExpressionFirst.prototype === ExpressionSecond.prototype);
        """;

    // Forward class references in closures are rejected by the checker. Keep
    // this runtime lifetime control on the verified unchecked harness path.
    public const string ForwardCaptureSource = """
        function make() {
            function before() { return C; }
            const arrowBefore = () => C;
            class C {}
            return [C, before, arrowBefore];
        }
        const first: any = make(), second: any = make();
        console.log(first[1]() === first[0], first[2]() === first[0],
            second[1]() === second[0], second[2]() === second[0], first[1]() === second[1]());
        """;

    public const string ForwardCaptureExpected = "true true true true false\n";

    [Theory, ModeData]
    public void ClosuresCreatedBeforeClassInitializationRetainTheirBinding(ExecutionMode mode)
        => Assert.Equal(ForwardCaptureExpected, TestHarness.RunUnchecked(ForwardCaptureSource, mode));

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", OriginalSource, "false\nfalse\nfalse false\nfalse false\n"];

        yield return ["issue2239-first-local-key", """
            function make() {
                const key = "method";
                class Local { [key]() { return 7; } }
                return Local;
            }
            const C = new make();
            console.log(typeof new C().method);
            """, "function\n"];

        yield return ["issue2239-local-keys-per-evaluation", """
            function make(key: string, value: number): any {
                class Local {
                    [key]() { return value; }
                    static [key]() { return value + 1; }
                }
                return Local;
            }
            const First: any = make("first", 3), Second: any = make("second", 7);
            const first: any = new First(), second: any = new Second();
            console.log(first.first(), second.second(), First.first(), Second.second());
            console.log(typeof first.second, typeof second.first);
            """, "3 7 4 8\nundefined undefined\n"];

        yield return ["issue2239-expression-and-global-controls", """
            const globalKey = "global";
            class Global { [globalKey]() { return 2; } }
            function make(): any {
                const key = "expression";
                return class { [key]() { return 3; } };
            }
            const C: any = make(), global: any = new Global();
            console.log(global.global(), new C().expression());
            """, "2 3\n"];

        yield return ["excluded-nominal-capture", """
            function make() {
                class C extends Array<any> {}
                const unused = () => C;
                return C;
            }
            console.log(typeof make());
            """, "function\n"];

        yield return ["static-new-this", """
            function make(): any {
                class C { constructor() {} static make(): any { return new this(); } }
                return C;
            }
            const First: any = make(), Second: any = make();
            const first: any = First.make(), second: any = Second.make();
            console.log(first instanceof First, second instanceof Second,
                first.constructor === First, second.constructor === Second);
            console.log(first instanceof Second, second instanceof First);
            """, "true true true true\nfalse false\n"];

        yield return ["static-and-instance-accessor-facets", """
            function make(): any {
                class C {
                    constructor() {}
                    static get value(): number { return 9; }
                    get value(): any { return true; }
                }
                return C;
            }
            const C: any = make();
            console.log(C.value, new C().value);
            """, "9 true\n"];

        yield return ["source-field-spelling", """
            function make(): any {
                class C { Upper = 3; URL = "url"; constructor() {} }
                return C;
            }
            const C: any = make(), instance: any = new C();
            console.log(instance.Upper, instance.URL);
            instance.Upper = 5; instance.URL = "changed";
            console.log(instance.Upper, instance.URL);
            """, "3 url\n5 changed\n"];

        yield return ["escaped-bindings-and-loops", """
            function make(): any {
                class C { constructor() {} }
                function before(): any { return C; }
                const arrowBefore = (): any => C;
                const after = (): any => C;
                return [C, before, arrowBefore, after];
            }
            const first = make(), second = make();
            console.log(first[1]() === first[0], first[2]() === first[0], first[3]() === first[0],
                second[1]() === second[0], second[2]() === second[0], second[3]() === second[0]);
            console.log(first[1]() === first[1](), first[1]() === second[1]());
            const makeArrow = () => {
                class Local { constructor() {} }
                return () => Local;
            };
            const readFirst = makeArrow(), readSecond = makeArrow();
            console.log(readFirst() === readFirst(), readFirst() === readSecond());
            const refs: any[] = [], values: any[] = [];
            for (let i = 0; i < 2; i++) {
                class Iteration { constructor() {} }
                refs.push(() => Iteration);
                values.push(Iteration);
            }
            console.log(refs[0]() === values[0], refs[1]() === values[1], refs[0]() === refs[1]());
            function shadowed(): any {
                class C {}
                const Outer = C, read = () => C;
                { class C {} console.log(C === Outer); }
                return read() === Outer;
            }
            console.log(shadowed());
            """, "true true true true true true\ntrue false\ntrue false\ntrue true false\nfalse\ntrue\n"];

        yield return ["definition-captures-and-mutation", """
            let definitions = 0;
            function make(seed: number): any {
                let value = seed;
                class C {
                    static initial = value;
                    static owner: any = C;
                    static { definitions++; }
                    field = value;
                    constructor() {}
                    read() { return value; }
                    static read() { return value; }
                }
                return { Ctor: C, set(next: number) { value = next; } };
            }
            const first = make(7), second = make(9);
            const First: any = first.Ctor, Second: any = second.Ctor;
            const one: any = new First(), two: any = new Second();
            console.log(First === Second, First.initial, Second.initial, definitions);
            console.log(First.owner === First, Second.owner === Second, one.field, two.field);
            first.set(11); second.set(13);
            const laterOne: any = new First(), laterTwo: any = new Second();
            console.log(one.field, two.field, laterOne.field, laterTwo.field,
                one.read(), two.read(), First.read(), Second.read());
            First.extra = 17; First.prototype.extra = 19; First.initial = 23;
            console.log(First.extra, Second.extra === undefined, one.extra,
                two.extra === undefined, First.initial, Second.initial, definitions);
            console.log(Object.getPrototypeOf(one) === First.prototype,
                Object.getPrototypeOf(two) === Second.prototype,
                First.prototype.constructor === First, Second.prototype.constructor === Second);
            """, "false 7 9 2\ntrue true 7 9\n7 9 11 13 11 13 11 13\n17 true 19 true 23 9 2\ntrue true true true\n"];

        yield return ["known-parent", """
            class Base {
                base: number;
                constructor(value: number = 3) { this.base = value; }
                parent() { return "base"; }
            }
            function make(seed: number): any {
                class Child extends Base {
                    child = seed;
                    constructor(value: number = seed) { super(value); }
                    owner(): any { return Child; }
                }
                return Child;
            }
            const First: any = make(29), Second: any = make(31);
            const one: any = new First(), two: any = new Second();
            console.log(First === Second, First.prototype === Second.prototype,
                Object.getPrototypeOf(First) === Base, Object.getPrototypeOf(Second) === Base);
            console.log(one.base, one.child, two.base, two.child, one.parent(), two.parent());
            console.log(one.owner() === First, two.owner() === Second,
                one instanceof Base, two instanceof Base, one instanceof Second, two instanceof First);
            console.log(Object.getPrototypeOf(one) === First.prototype,
                Object.getPrototypeOf(First.prototype) === Base.prototype,
                First.prototype.constructor === First, Second.prototype.constructor === Second);
            class GenericBase<T> {
                Upper: T;
                constructor(value: T) { this.Upper = value; }
            }
            function makeGeneric(value: number): any {
                class Child extends GenericBase<number> { constructor() { super(value); } }
                return Child;
            }
            const GenericFirst: any = makeGeneric(113), GenericSecond: any = makeGeneric(127);
            console.log(new GenericFirst().Upper, new GenericSecond().Upper, new GenericFirst().Upper);
            """, "false false true true\n29 29 31 31 base base\ntrue true true true false false\ntrue true true true\n113 127 113\n"];

        yield return ["runtime-parent", """
            class Left { label = "left"; constructor() {} }
            class Right { label = "right"; constructor() {} }
            function make(Parent: any, value: number): any {
                class Child extends Parent {
                    value = value;
                    constructor() { super(); }
                }
                return Child;
            }
            const First: any = make(Left, 37), Second: any = make(Right, 41);
            const one: any = new First(), two: any = new Second(), later: any = new First();
            console.log(First === Second, First.prototype === Second.prototype,
                Object.getPrototypeOf(First) === Left, Object.getPrototypeOf(Second) === Right);
            console.log(one.label, one.value, two.label, two.value, later.label, later.value);
            console.log(one instanceof Left, one instanceof Right, two instanceof Left, two instanceof Right,
                one instanceof Second, two instanceof First);
            console.log(Object.getPrototypeOf(First.prototype) === Left.prototype,
                Object.getPrototypeOf(Second.prototype) === Right.prototype);
            """, "false false true true\nleft 37 right 41 left 37\ntrue false false true false false\ntrue true\n"];

        yield return ["constructor-arguments-and-new-target", """
            function make(seed: number): any {
                class C {
                    value: any;
                    rest: any[];
                    count: number;
                    target: any;
                    constructor(value: number = seed, ...rest: any[]) {
                        this.value = value; this.rest = rest;
                        this.count = arguments.length; this.target = new.target;
                    }
                }
                return C;
            }
            const First: any = make(43), Second: any = make(47);
            const one: any = new First(), two: any = new Second(undefined, 53, 59);
            const later: any = new First(61, 67);
            console.log(one.value, one.rest.length, one.count, one.target === First);
            console.log(two.value, two.rest.join(","), two.count, two.target === Second);
            console.log(later.value, later.rest.join(","), later.count, later.target === First);
            console.log(First.name, First.length, Second.name, Second.length,
                one instanceof First, one instanceof Second, two instanceof First, two instanceof Second);
            """, "43 0 0 true\n47 53,59 3 true\n61 67 2 true\nC 0 C 0 true false false true\n"];

        yield return ["async-local-lifetime", """
            async function make(value: number): Promise<any> {
                await Promise.resolve(0);
                class C {
                    field = value;
                    constructor() {}
                }
                const read = () => C;
                await Promise.resolve(0);
                return [C, read];
            }
            const makeArrow = async (value: number): Promise<any> => {
                class C { field = value; constructor() {} }
                await Promise.resolve(0);
                return C;
            };
            async function run() {
                const first: any = await make(71), second: any = await make(73);
                const First: any = first[0], Second: any = second[0];
                console.log(First === Second, First.prototype === Second.prototype,
                    first[1]() === First, second[1]() === Second);
                console.log(new First().field, new Second().field,
                    new First() instanceof Second, new Second() instanceof First);
                const ArrowFirst: any = await makeArrow(79), ArrowSecond: any = await makeArrow(83);
                console.log(ArrowFirst === ArrowSecond, ArrowFirst.prototype === ArrowSecond.prototype,
                    new ArrowFirst().field, new ArrowSecond().field);
            }
            run();
            """, "false false true true\n71 73 false false\nfalse false 79 83\n"];

        yield return ["generator-local-lifetime", """
            function* make(value: number): Generator<any, any, any> {
                class C { field = value; constructor() {} }
                yield C;
                yield () => C;
                return C as any;
            }
            async function* makeAsync(value: number): AsyncGenerator<any, any, any> {
                await Promise.resolve(0);
                class C { field = value; constructor() {} }
                yield C;
                await Promise.resolve(0);
                yield () => C;
            }
            async function run() {
                const one: any = make(89), two: any = make(97);
                const First: any = one.next().value, Second: any = two.next().value;
                const readFirst: any = one.next().value, readSecond: any = two.next().value;
                console.log(First === Second, First.prototype === Second.prototype,
                    readFirst() === First, readSecond() === Second);
                console.log(one.next().value === First, two.next().value === Second,
                    new First().field, new Second().field, new First() instanceof Second);
                const asyncOne: any = makeAsync(101), asyncTwo: any = makeAsync(103);
                const AsyncFirst: any = (await asyncOne.next()).value;
                const AsyncSecond: any = (await asyncTwo.next()).value;
                const asyncReadFirst: any = (await asyncOne.next()).value;
                const asyncReadSecond: any = (await asyncTwo.next()).value;
                console.log(AsyncFirst === AsyncSecond, AsyncFirst.prototype === AsyncSecond.prototype,
                    asyncReadFirst() === AsyncFirst, asyncReadSecond() === AsyncSecond,
                    new AsyncFirst().field, new AsyncSecond().field);
            }
            run();
            """, "false false true true\ntrue true 89 97 false\nfalse false true true 101 103\n"];

        yield return ["direct-construction-across-suspension", """
            let trace = "";
            function argument(value: any): any { trace += "a"; return value; }
            function* make(): Generator<any, any, any> {
                class C {
                    static { trace += "g"; }
                    value: any;
                    target: any;
                    constructor(value: any) { trace += "c"; this.value = value; this.target = new.target; }
                }
                const value = new C(yield argument("argument"));
                console.log(value.target === C, value.value, trace);
                return value as any;
            }
            async function makeAsync(pending: Promise<any>) {
                class C {
                    static { trace += "u"; }
                    value: any;
                    target: any;
                    constructor(value: any) { trace += "c"; this.value = value; this.target = new.target; }
                }
                const value = new C(await argument(pending));
                console.log(value.target === C, value.value, trace);
            }
            async function run() {
                const iterator: any = make();
                console.log(iterator.next().value, trace);
                const completed: any = iterator.next("sent");
                console.log(completed.done, completed.value.value, trace);
                trace = "";
                let release: any;
                const pending = new Promise<any>(resolve => { release = resolve; });
                const completion = makeAsync(pending);
                console.log("pending", trace);
                release("awaited");
                await completion;
            }
            run();
            """, "argument ga\ntrue sent gac\ntrue sent gac\npending ua\ntrue awaited uac\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void EachEvaluationCreatesItsOwnConstructorAndPrototype(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    public const string ModuleExpected = "false false false false\n107 109 107\ntrue true true true\n";

    public static Dictionary<string, string> ModuleFiles() => new()
    {
        ["factory.ts"] = """
            export function make(value: number): any {
                class C {
                    static initial = value;
                    field = value;
                    constructor() {}
                    owner(): any { return C; }
                }
                return C;
            }
            """,
        ["main.ts"] = """
            import { make as create } from './factory';
            const First: any = create(107), Second: any = create(109);
            const one: any = new First(), two: any = new Second(), later: any = new First();
            console.log(First === Second, First.prototype === Second.prototype,
                one instanceof Second, two instanceof First);
            console.log(one.field, two.field, later.field);
            console.log(one.owner() === First, two.owner() === Second,
                First.initial === 107, Second.initial === 109);
            """
    };

    [Theory, ModeData]
    public void ImportedFactoryCreatesIndependentDefinitions(ExecutionMode mode)
        => Assert.Equal(ModuleExpected, TestHarness.RunModules(ModuleFiles(), "main.ts", mode));
}
