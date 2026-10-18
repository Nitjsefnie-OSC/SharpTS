using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class HeldClassConstructorTests
{
    private const string TypedAliases = """
        class Target {
            static tag: string = "target";
            value: number;
            constructor(value: number) { this.value = value; }
            read(): number { return this.value; }
        }
        class Source {
            static tag: string = "source";
            value: number;
            constructor(value: number) { this.value = value + 1; }
            read(): number { return this.value; }
        }
        let Alias: typeof Target = Target;
        Alias = Source as any;
        const selected = new Alias(7);
        console.log(Alias === Source, Alias !== Target, Alias.tag, selected.read(),
            selected instanceof Source, selected instanceof Target);
        function construct(C: typeof Target) { return new C(3); }
        console.log(construct(Source).read());
        const holder: { Constructor: typeof Target } = { Constructor: Source };
        console.log(new holder.Constructor(5).read());
        """;

    private const string ShadowedDeclaration = """
        class Target {
            value: number;
            constructor(value: number) { this.value = value; }
            read(): number { return this.value; }
        }
        class Source {
            value: number;
            constructor(value: number) { this.value = value + 1; }
            read(): number { return this.value; }
        }
        type TargetConstructor = typeof Target;
        function construct(Target: TargetConstructor): number {
            const selected = new Target(3);
            return selected.read();
        }
        console.log(construct(Source));
        """;

    private const string ExplicitLocal = """
        class Target {
            value: number;
            constructor(value: number) { this.value = value; }
            read(): number { return this.value; }
        }
        class Source {
            value: number;
            constructor(value: number) { this.value = value + 1; }
            read(): number { return this.value; }
        }
        let Alias: typeof Target = Source;
        const selected = new (Alias as any)(7) as Target;
        console.log(selected.read(), selected.value, selected instanceof Source);
        """;

    private const string ExplicitReturn = """
        class Target {
            value: number;
            constructor(value: number) { this.value = value; }
            read(): number { return this.value; }
        }
        class Source {
            value: number;
            constructor(value: number) { this.value = value + 1; }
            read(): number { return this.value; }
        }
        function construct(C: typeof Target): Target {
            return new (C as any)(3) as Target;
        }
        const selected = construct(Source);
        console.log(selected.read(), selected instanceof Source);
        """;

    private const string AncestorStorage = """
        class Base { x = 1; }
        class Target extends Base { y = 2; }
        class Source { x = 3; y = 4; }
        const Held: typeof Target = Source;
        function pass(value: Base): Base { return value; }
        function read(value: Base): number { return value.x; }
        class Holder {
            value: Base;
            constructor(value: Base) { this.value = value; }
        }
        const value: Target = new Held();
        const items: Base[] = [value];
        console.log(read(pass(value)), new Holder(value).value.x, items[0].x,
            value.y, value instanceof Source);
        """;

    private const string ConstrainedStorage = """
        class Base { x = 1; }
        class Target extends Base { y = 2; }
        class Source { x = 3; y = 4; }
        const Held: typeof Target = Source;
        class Box<T extends Base> {
            value: T;
            constructor(value: T) { this.value = value; }
            read(): T { return this.value; }
        }
        const box = new Box<Target>(new Held());
        console.log(box.read().x, box.read().y, box.read() instanceof Source);
        """;

    private const string EvaluationAndSuspension = """
        let trace = "";
        class Target {
            value: number;
            constructor(value = 0, ...rest: number[]) { this.value = value; }
        }
        class Source {
            value: number;
            constructor(value = 10, ...rest: number[]) {
                trace += "C";
                this.value = value + rest.length;
            }
        }
        let Held: typeof Target = Source;
        const holder = {
            get Constructor(): typeof Target { trace += "G"; return Held; }
        };
        function first(): number { trace += "A"; Held = Target; return 3; }
        function tail(): number { trace += "S"; return 4; }
        const selected = new holder.Constructor(first(), ...[tail(), 5]);
        console.log(trace, selected.value, selected instanceof Source, selected instanceof Target);

        const marker = {};
        function fail(): number { trace += "X"; throw marker; }
        trace = "";
        Held = Source;
        try { new holder.Constructor(fail(), ...[tail()]); }
        catch (error) { console.log(error === marker, trace); }

        trace = "";
        Held = Source;
        const defaulted = new holder.Constructor();
        console.log(trace, defaulted.value, defaulted instanceof Source);

        async function main(): Promise<void> {
            trace = "";
            Held = Source;
            const resumed = new holder.Constructor(await Promise.resolve().then(() => {
                trace += "W";
                Held = Target;
                return 7;
            }), ...[tail()]);
            console.log(trace, resumed.value, resumed instanceof Source);
        }
        main();
        """;

    private const string OwnedGeneric = """
        class Target<T> {
            value: T;
            constructor(value: T) { this.value = value; }
            read(): T { return this.value; }
        }
        class Source<T> {
            value: T;
            constructor(value: T) { this.value = value; }
            read(): T { return this.value; }
        }
        const Held: typeof Target = Source;
        function owned<T>(value: T): T { return new Held<T>(value).read(); }
        const selected = new Held<number>(7);
        console.log(selected.read(), selected instanceof Source, selected instanceof Target,
            owned<string>("ok"), owned<number>(9), new Date(0).getTime());
        """;

    private const string ConstructorReceiver = """
        class Target {
            value = 1;
            read = () => this.value;
        }
        class Source {
            value = 2;
            read = () => this.value;
        }
        const Held: typeof Target = Source;
        const selected = new Held();
        console.log(selected.read(), selected instanceof Source);
        class Derived extends Target {
            extra = 5;
            constructor() { super(); this.value += this.extra; }
        }
        const derived = new Derived();
        console.log(derived.read(), derived.extra,
            derived instanceof Derived, derived instanceof Target);
        """;

    private const string ActualGenericArity = """
        class Target<T> { constructor(public value: T) {} }
        class Source<T, U = string> {
            extra: U = "source" as any;
            constructor(public value: T) {}
        }
        const Held: typeof Target = Source;
        const value = new Held<number>(47);
        console.log(value.value, (value as any).extra, value instanceof Source);
        """;

    private const string CanonicalLibrary = """
        export class Declared { value = 4; }
        export namespace Space { export class Nested { value = 5; } }
        export class Box<T> {
            value: T;
            constructor(value: T) { this.value = value; }
        }
        """;

    private const string CanonicalMain = """
        import { Declared as Imported, Space, Box } from "./lib";
        class Local { value = 7; }
        console.log(new Imported().value, new Space.Nested().value,
            new Box<number>(6).value, new Local().value, new Date(0).getTime());
        """;

    public static IEnumerable<object[]> SavedCases()
    {
        yield return Case("typed-aliases", TypedAliases, "true true source 8 true false\n4\n6\n");
        yield return Case("shadowed-declaration", ShadowedDeclaration, "4\n");
        yield return Case("explicit-local", ExplicitLocal, "8 8 true\n");
        yield return Case("explicit-return", ExplicitReturn, "4 true\n");
        yield return Case("ancestor-storage", AncestorStorage, "3 3 3 4 true\n");
        yield return Case("constrained-storage", ConstrainedStorage, "3 4 true\n");
        yield return Case("evaluation-and-suspension", EvaluationAndSuspension,
            "GASC 5 true false\ntrue GX\nGC 10 true\nGWSC 8 true\n");
        yield return Case("owned-generic", OwnedGeneric, "7 true false ok 9 0\n");
        yield return Case("actual-generic-arity", ActualGenericArity, "47 source true\n");
        yield return Case("actual-generic-parameter-order", """
            class Target<T, U> { constructor(public value: U) {} }
            class Source<S> { constructor(public value: S) {} }
            const Held: typeof Target = Source;
            const value = new Held<number, string>("ok");
            console.log(value.value, value instanceof Source);
            """, "ok true\n");
        yield return Case("actual-generic-without-type-arguments", """
            class Target { value = "target"; }
            class Source<T = string> { value = "source"; }
            // #2544: the checker rejects this legal generic-to-nongeneric assignment.
            // Keep its independent limitation out of the runtime constructor regression.
            const Held: typeof Target = Source as any;
            const value = new Held();
            console.log(value.value, value instanceof Source);
            """, "source true\n");
        yield return Case("constructor-receiver", ConstructorReceiver, "2 true\n6 5 true true\n");
        yield return Case("canonical-imports", CanonicalMain, "4 5 6 7 0\n",
            new Dictionary<string, string> { ["lib.ts"] = CanonicalLibrary });
    }

    public static IEnumerable<object[]> ModeCases() => SavedCases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ConstructionUsesTheHeldValueAcrossCheckedStorageAndSuspension(
        string name, Dictionary<string, string> files, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(files, "main.ts", mode));
    }

    private static object[] Case(string name, string source, string expected,
        Dictionary<string, string>? additionalFiles = null)
    {
        var files = additionalFiles ?? new Dictionary<string, string>();
        files["main.ts"] = source;
        return [name, files, expected];
    }
}
