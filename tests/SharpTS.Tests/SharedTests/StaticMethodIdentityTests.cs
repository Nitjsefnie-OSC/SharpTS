using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticMethodIdentityTests
{
    [Theory, ModeData]
    public void ReadsDescriptorsAndInheritanceShareCanonicalMethods(ExecutionMode mode)
    {
        const string source = """
            class Base {
                static plain() { return 1; }
                static async asynchronous() { return 1; }
                static *generator() { yield 1; }
                static async *asyncGenerator() { yield 1; }
            }
            class Derived extends Base {}
            const base: any = Base, derived: any = Derived;
            for (const key of ["plain", "asynchronous", "generator", "asyncGenerator"]) {
                const descriptor: any = Object.getOwnPropertyDescriptor(base, key);
                console.log(base[key] === base[key], descriptor.value === base[key], base[key] === derived[key]);
                console.log(Object.hasOwn(derived, key));
            }
            """;

        Assert.Equal(string.Concat(Enumerable.Repeat("true true true\nfalse\n", 4)), TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassExpressionsOwnIndependentStableMethods(ExecutionMode mode)
    {
        const string source = """
            function make() {
                return class {
                    static plain() { return 1; }
                    static async asynchronous() { return 1; }
                    static *generator() { yield 1; }
                    static async *asyncGenerator() { yield 1; }
                };
            }
            const first: any = make(), second: any = make();
            for (const key of ["plain", "asynchronous", "generator", "asyncGenerator"]) {
                console.log(first[key] === first[key], first[key] === second[key]);
            }
            """;

        Assert.Equal(string.Concat(Enumerable.Repeat("true false\n", 4)), TestHarness.Run(source, mode));
    }

    [Fact]
    public void SymbolMethodsKeepIdentityAcrossReadsAndInheritance()
    {
        // Interpreter symbol-read identity control. Compiled static symbol reads
        // currently create fresh callable wrappers, tracked separately in #2017.
        const string source = """
            const plain = Symbol("plain"), asynchronous = Symbol("async");
            const generator = Symbol("generator"), asyncGenerator = Symbol("asyncGenerator");
            class Base {
                static [plain]() { return 1; }
                static async [asynchronous]() { return 1; }
                static *[generator]() { yield 1; }
                static async *[asyncGenerator]() { yield 1; }
            }
            class Derived extends Base {}
            const base: any = Base, derived: any = Derived;
            for (const key of [plain, asynchronous, generator, asyncGenerator]) {
                console.log(base[key] === base[key], base[key] === derived[key]);
            }
            """;

        Assert.Equal(string.Concat(Enumerable.Repeat("true true\n", 4)), TestHarness.RunInterpreted(source));
    }

    [Theory, ModeData]
    public void PlainMethodsReceiveThisOnlyWhenCalled(ExecutionMode mode)
    {
        const string source = """
            class Base { static receiver() { return this; } }
            class Derived extends Base {}
            const base: any = Base, derived: any = Derived;
            const receiver: any = base.receiver;
            const copied: any = { receiver };
            console.log(base.receiver() === base, derived["receiver"]() === derived);
            console.log(receiver() === undefined, copied.receiver() === copied);
            console.log(receiver.call(copied) === copied, receiver.apply(derived, []) === derived);
            console.log(receiver.bind(copied)() === copied);
            console.log(receiver.call(null) === null, receiver.apply(undefined, []) === undefined);
            console.log(receiver.bind(null)() === null, receiver.bind(undefined)() === undefined);
            console.log(receiver.call(7) === 7, receiver.apply("value", []) === "value", receiver.bind(false)() === false);
            console.log(receiver.bind(null)(...[]) === null);
            """;

        Assert.Equal("true true\ntrue true\ntrue true\ntrue\ntrue true\ntrue true\ntrue true true\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PlainMethodsDefaultOmittedReceiverToUndefined(ExecutionMode mode)
    {
        const string source = """
            class C { static receiver() { return this; } }
            const receiver: any = C.receiver;
            console.log(receiver.call() === undefined, receiver.apply() === undefined, receiver.bind()() === undefined);
            """;

        Assert.Equal("true true true\n", TestHarness.Run(source, mode));
    }

    internal const string BorrowedApplySource = """
        class C { static receiver() { return this; } }
        const f: any = C.receiver;
        const prototype: any = Function.prototype;
        console.log(f.call() === undefined, f.apply() === undefined, f.bind()() === undefined);
        console.log(prototype.apply.call(f) === undefined,
            Reflect.apply(prototype.apply, f, []) === undefined,
            prototype.call.call(f) === undefined);
        console.log(prototype.apply.call(f, null) === null,
            Reflect.apply(prototype.apply, f, [undefined, null]) === undefined,
            Reflect.apply(prototype.apply, f, [7, []]) === 7);
        console.log(Reflect.apply(prototype.apply, f, ["value", []]) === "value",
            Reflect.apply(prototype.apply, f, [false, []]) === false,
            f.bind()() === undefined, f.bind(null)() === null);
        """;

    internal const string BorrowedApplyExpected = "true true true\ntrue true true\ntrue true true\ntrue true true true\n";

    [Theory, ModeData]
    public void BorrowedApplyDefaultsItsReceiverWithoutChangingExplicitValues(ExecutionMode mode)
    {
        Assert.Equal(BorrowedApplyExpected, TestHarness.Run(BorrowedApplySource, mode));
    }

    [Theory, ModeData]
    public void CanonicalMethodRetainsAssignedProperties(ExecutionMode mode)
    {
        const string source = """
            class C { static plain() { return 1; } static async asynchronous() { return 1; } }
            const klass: any = C;
            klass.plain.tag = "plain";
            klass.asynchronous.tag = "async";
            console.log(klass.plain.tag, klass.asynchronous.tag);
            const plain: any = Object.getOwnPropertyDescriptor(klass, "plain").value;
            const asynchronous: any = Object.getOwnPropertyDescriptor(klass, "asynchronous").value;
            console.log(plain === klass.plain, asynchronous === klass.asynchronous, plain.tag, asynchronous.tag);
            """;

        Assert.Equal("plain async\ntrue true plain async\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AsyncAndGeneratorMethodsReceiveThisOnlyWhenCalled(ExecutionMode mode)
    {
        const string source = """
            class Base {
                static async asynchronous() { await Promise.resolve(0); return this; }
                static *generator() { yield this; }
                static async *asyncGenerator() { await Promise.resolve(0); yield this; }
            }
            class Derived extends Base {}
            const base: any = Base, derived: any = Derived;
            const asynchronous: any = base.asynchronous;
            const generator: any = base.generator;
            const asyncGenerator: any = base.asyncGenerator;
            const copied: any = { asynchronous, generator, asyncGenerator };
            const asynchronousNull = asynchronous.bind(null)();
            const generatorNull = generator.bind(null)();
            const asyncGeneratorNull = asyncGenerator.bind(null)();
            async function run() {
                console.log((await asynchronousNull) === null, generatorNull.next().value === null, (await asyncGeneratorNull.next()).value === null);
                console.log((await base.asynchronous()) === base, (await derived["asynchronous"]()) === derived);
                console.log((await asynchronous()) === undefined, (await copied.asynchronous()) === copied);
                console.log((await asynchronous.call(copied)) === copied);
                console.log((await asynchronous.call(null)) === null, (await asynchronous.apply(undefined, [])) === undefined, (await asynchronous.call(7)) === 7);
                console.log((await asynchronous.bind(null)()) === null, (await asynchronous.bind(undefined)()) === undefined, (await asynchronous.bind(false)()) === false);
                console.log((await asynchronous.apply("value", [])) === "value");
                console.log(base.generator().next().value === base, derived["generator"]().next().value === derived);
                console.log(generator().next().value === undefined, copied.generator().next().value === copied);
                console.log(generator.call(copied).next().value === copied);
                console.log(generator.call(null).next().value === null, generator.apply(undefined, []).next().value === undefined, generator.call(7).next().value === 7);
                console.log(generator.bind(null)().next().value === null, generator.bind(undefined)().next().value === undefined, generator.bind(false)().next().value === false);
                console.log(generator.apply("value", []).next().value === "value");
                console.log((await base.asyncGenerator().next()).value === base, (await derived["asyncGenerator"]().next()).value === derived);
                console.log((await asyncGenerator().next()).value === undefined, (await copied.asyncGenerator().next()).value === copied);
                console.log((await asyncGenerator.call(copied).next()).value === copied);
                console.log((await asyncGenerator.call(null).next()).value === null, (await asyncGenerator.apply(undefined, []).next()).value === undefined, (await asyncGenerator.call(7).next()).value === 7);
                console.log((await asyncGenerator.bind(null)().next()).value === null, (await asyncGenerator.bind(undefined)().next()).value === undefined, (await asyncGenerator.bind(false)().next()).value === false);
                console.log((await asyncGenerator.apply("value", []).next()).value === "value");
            }
            run();
            """;

        Assert.Equal("true true true\n" + string.Concat(Enumerable.Repeat("true true\ntrue true\ntrue\ntrue true true\ntrue true true\ntrue\n", 3)), TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AsyncAndGeneratorMethodsDefaultOmittedReceiverToUndefined(ExecutionMode mode)
    {
        const string source = """
            class Base {
                static async asynchronous() { await Promise.resolve(0); return this; }
                static *generator() { yield this; }
                static async *asyncGenerator() { await Promise.resolve(0); yield this; }
            }
            const base: any = Base;
            const asynchronous: any = base.asynchronous;
            const generator: any = base.generator;
            const asyncGenerator: any = base.asyncGenerator;
            async function run() {
                console.log((await asynchronous.call()) === undefined, (await asynchronous.apply()) === undefined, (await asynchronous.bind()()) === undefined);
                console.log(generator.call().next().value === undefined, generator.apply().next().value === undefined, generator.bind()().next().value === undefined);
                console.log((await asyncGenerator.call().next()).value === undefined, (await asyncGenerator.apply().next()).value === undefined, (await asyncGenerator.bind()().next()).value === undefined);
            }
            run();
            """;
        Assert.Equal("true true true\ntrue true true\ntrue true true\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void CapturedLocalsSurviveMemberAndExtractedCalls()
    {
        // Interpreter scope-alignment control: resolved outer locals must keep
        // their depth when a canonical static method is invoked without a receiver.
        const string source = """
            function make(label: string) {
                class C {
                    static plain() { return label; }
                    static async asynchronous() { await Promise.resolve(0); return label; }
                    static *generator() { yield label; }
                    static async *asyncGenerator() { await Promise.resolve(0); yield label; }
                    static #privateRead() { return label; }
                    static readPrivate() { return C.#privateRead(); }
                }
                return C;
            }
            const first: any = make("first"), second: any = make("second");
            const plain: any = first.plain, asynchronous: any = first.asynchronous;
            const generator: any = first.generator, asyncGenerator: any = first.asyncGenerator;
            async function run() {
                console.log(first.plain(), plain(), second.plain());
                console.log(await first.asynchronous(), await asynchronous(), await second.asynchronous());
                console.log(first.generator().next().value, generator().next().value, second.generator().next().value);
                console.log((await first.asyncGenerator().next()).value, (await asyncGenerator().next()).value, (await second.asyncGenerator().next()).value);
                console.log(first.readPrivate(), second.readPrivate());
            }
            run();
            """;

        Assert.Equal(string.Concat(Enumerable.Repeat("first first second\n", 4)) + "first second\n", TestHarness.RunInterpreted(source));
    }

    [Theory, ModeData]
    public void StaticIteratorMethodsBindClassCallsAndCopiedProtocols(ExecutionMode mode)
    {
        const string source = """
            class Base {
                static *[Symbol.iterator]() { yield this; }
                static async *[Symbol.asyncIterator]() { await Promise.resolve(0); yield this; }
            }
            class Derived extends Base {}
            const klass: any = Derived;
            const copied: any = {};
            copied[Symbol.iterator] = klass[Symbol.iterator];
            copied[Symbol.asyncIterator] = klass[Symbol.asyncIterator];
            console.log(klass[Symbol.iterator]().next().value === klass);
            for (const receiver of copied) console.log(receiver === copied);
            async function run() {
                console.log((await klass[Symbol.asyncIterator]().next()).value === klass);
                for await (const receiver of copied) console.log(receiver === copied);
            }
            run();
            """;

        Assert.Equal("true\ntrue\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void LoopClassMethodsKeepTheirDefinitionEnvironment()
    {
        // Interpreter scope-alignment control for distinct loop environments.
        // Stable identity across repeated definitions is covered in both modes above.
        const string source = """
            const classes: any[] = [];
            for (let index = 0; index < 2; index++) {
                class C {
                    static plain() { return index; }
                    static async asynchronous() { await Promise.resolve(0); return index; }
                    static *generator() { yield index; }
                    static async *asyncGenerator() { await Promise.resolve(0); yield index; }
                }
                classes.push(C);
            }
            async function run() {
                for (const C of classes) {
                    const plain: any = C.plain, asynchronous: any = C.asynchronous;
                    const generator: any = C.generator, asyncGenerator: any = C.asyncGenerator;
                    console.log(plain(), await asynchronous(), generator().next().value, (await asyncGenerator().next()).value);
                }
            }
            run();
            """;

        Assert.Equal("0 0 0 0\n1 1 1 1\n", TestHarness.RunInterpreted(source));
    }

    [Theory, ModeData]
    public void DotNetExtensionFallbackSuppliesStaticReceiver(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["main.ts"] = """
                import "dotnet-extensions:System.Linq.Enumerable";
                class C { static receiver() { return this; } }
                const klass: any = C;
                console.log(C.receiver() === C);
                async function run() {
                    await Promise.resolve(0);
                    console.log(C.receiver() === C, klass["receiver"]() === C);
                }
                run();
                """,
        };

        Assert.Equal("true\ntrue true\n", TestHarness.RunModules(files, "main.ts", mode));
    }
}
