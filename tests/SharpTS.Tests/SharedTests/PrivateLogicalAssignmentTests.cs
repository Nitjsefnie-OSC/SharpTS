using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrivateLogicalAssignmentTests
{
    [Theory, ModeData]
    public void OperatorsShortCircuitAndEvaluateTheReceiverOnce(ExecutionMode mode)
    {
        const string source = """
            class C {
                #or = 0;
                #and = 1;
                #nullish: number | undefined = undefined;
                run() {
                    let receivers = 0;
                    let rights = 0;
                    const select = () => { receivers++; return this; };
                    const seven = () => { rights++; return 7; };
                    const zero = () => { rights++; return 0; };
                    console.log(select().#or ||= seven(), select().#or ||= seven());
                    console.log(select().#and &&= zero(), select().#and &&= seven());
                    console.log(select().#nullish ??= zero(), select().#nullish ??= seven());
                    console.log(receivers, rights);
                    console.log(this.#or, this.#and, this.#nullish);
                }
            }
            new C().run();
            """;
        Assert.Equal("7 7\n0 0\n0 0\n6 3\n7 0 0\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void StaticFieldsAndRepeatedClassExpressionsKeepTheirBrands(ExecutionMode mode)
    {
        const string source = """
            class Static {
                static #or = 0;
                static #and = 1;
                static #nullish: any = null;
                static run() {
                    console.log(this.#or ||= 2, this.#and &&= 0, this.#nullish ??= false);
                    console.log(this.#or ||= 9, this.#and &&= 9, this.#nullish ??= 9);
                }
            }
            Static.run();
            function create(initial: number) {
                return class { #value = initial; run() { return this.#value ||= 2; } };
            }
            const First = create(0);
            const Second = create(5);
            console.log(new First().run(), new Second().run());
            """;
        Assert.Equal("2 0 false\n2 0 false\n2 5\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void RhsMutationDoesNotReplaceTheOriginalReference(ExecutionMode mode)
    {
        const string source = """
            class C {
                #value = 0;
                run(other: C) {
                    let current = this;
                    const next = () => { current = other; return 9; };
                    console.log(current.#value ||= next(), this.#value, other.#value, current === other);
                }
            }
            new C().run(new C());
            """;
        Assert.Equal("9 9 0 true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AwaitAndYieldOnlyRunOnTheWriteBranch(ExecutionMode mode)
    {
        const string source = """
            let calls = 0;
            async function next() { calls++; return 7; }
            class C {
                #value: number | undefined = undefined;
                async run() { return this.#value ??= await next(); }
                *generate() { return this.#value ??= yield 3; }
            }
            async function main() {
                const first = new C();
                console.log(await first.run(), await first.run(), calls);
                const second = new C();
                const iterator = second.generate();
                console.log(iterator.next().value);
                console.log(iterator.next(9).value);
                const completed = second.generate().next();
                console.log(completed.value, completed.done);
            }
            main();
            """;
        Assert.Equal("7 7 1\n3\n9\n9 true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void WrongBrandThrowsBeforeTheRhsAndThrownRhsDoesNotWrite(ExecutionMode mode)
    {
        const string source = """
            let calls = 0;
            function fail(): any { calls++; throw "failed"; }
            class C {
                #value = 0;
                run(target: any) { return target.#value ||= ++calls; }
                throwing() {
                    try { this.#value ||= fail(); } catch (error) { console.log(error, this.#value, calls); }
                }
            }
            const value = new C();
            try { value.run({}); } catch (error) { console.log(error instanceof TypeError, calls); }
            value.throwing();
            """;
        Assert.Equal("true 0\nfailed 0 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AnyReceiversKeepTheirDynamicWriteTypes(ExecutionMode mode)
    {
        const string source = """
            class C {
                readonly #value: number = 0;
                run(target: any) { return target.#value ||= "dynamic"; }
            }
            const value = new C();
            console.log(value.run(value), value.run(value));
            """;
        Assert.Equal("dynamic dynamic\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void GenericPrivateFieldsAndReadonlyConstructorInitializationWork(ExecutionMode mode)
    {
        const string source = """
            class Box<T> {
                #value: T | undefined = undefined;
                run(value: T): T { return this.#value ??= value; }
            }
            const box = new Box<number>();
            console.log(box.run(7), box.run(9));
            class Readonly {
                readonly #value: number = 0;
                constructor() { this.#value ||= 2; }
                read() { return this.#value; }
            }
            console.log(new Readonly().read());
            """;
        Assert.Equal("7 7\n2\n", TestHarness.Run(source, mode));
    }

    public static IEnumerable<object[]> InvalidCases()
    {
        string[] sources =
        [
            "class C { #value = 0; run() { this.#value ||= 'bad'; } }",
            "class C { readonly #value = 0; run() { this.#value ||= 2; } }",
            "class C { readonly #value = 0; constructor() { const other = this; other.#value ||= 2; } }",
            "class C { readonly #value = 0; constructor() { const write = () => this.#value ||= 2; write(); } }",
            "class C { #method() {} run() { this.#method ||= () => {}; } }",
            "class C { run() { this.#missing ||= 2; } }",
        ];
        foreach (string source in sources)
            foreach (ExecutionMode mode in new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
                yield return [source, mode];
    }

    [Theory, MemberData(nameof(InvalidCases))]
    public void InvalidPrivateWritesAreRejected(string source, ExecutionMode mode) =>
        Assert.IsAssignableFrom<TypeCheckException>(Assert.ThrowsAny<Exception>(() => TestHarness.Run(source, mode)));

    [Theory, ModeData]
    public void GenericPrivateWritesUseTheInstantiatedFieldType(ExecutionMode mode)
    {
        // Module checking supplies the generic self binding before method annotations
        // are resolved, matching the checked CLI's class preparation pass.
        string[] sources =
        [
            "class C<T> { #value: (value: T) => T = (value: T) => value; run(other: C<number>) { other.#value ||= {}; } }",
            "class C<T> { #value: T | undefined = undefined; run(other: C<number>) { other.#value ??= 'bad'; } }",
        ];
        foreach (string source in sources)
        {
            var files = new Dictionary<string, string> { ["main.ts"] = source };
            Assert.Contains("Cannot assign", Assert.ThrowsAny<Exception>(() =>
                TestHarness.RunModules(files, "main.ts", mode)).Message);
        }
    }
}
