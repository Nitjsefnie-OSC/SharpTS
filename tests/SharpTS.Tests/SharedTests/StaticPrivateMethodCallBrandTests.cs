using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticPrivateMethodCallBrandTests
{
    public const string IssueSource = """
        class C {
            static #m() { return 42; }
            static B = class {
                static read(object: typeof C) { return object.#m(); }
            };
        }
        const reader: any = C.B;
        console.log(reader.read(C));
        try { reader.read(reader); } catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string BrandsSource = """
        let argumentsEvaluated = 0, bodiesEntered = 0;
        class C {
            static #sync(value: any) { bodiesEntered++; return value; }
            static async #async(value: any) { bodiesEntered++; await Promise.resolve(0); return value; }
            static *#generator(value: any) { bodiesEntered++; yield value; }
            static async *#asyncGenerator(value: any) { bodiesEntered++; await Promise.resolve(0); yield value; }
            static Inner = class {
                static sync(object: typeof C) { return object.#sync((argumentsEvaluated++, 5)); }
                static asynchronous(object: typeof C) { return object.#async((argumentsEvaluated++, 5)); }
                static generator(object: typeof C) { return object.#generator((argumentsEvaluated++, 5)); }
                static asyncGenerator(object: typeof C) { return object.#asyncGenerator((argumentsEvaluated++, 5)); }
            };
        }
        class Child extends C {}
        class Other {}
        async function run() {
            const reader: any = C.Inner;
            const invalid: any[] = [Child, reader, Other, {}, new C(), null, undefined,
                1, "value", false, Symbol("value"), 1n];
            const readers: any[] = [reader.sync, reader.asynchronous, reader.generator, reader.asyncGenerator];
            let rejected = 0, returned = 0;
            for (const object of invalid) {
                for (const read of readers) {
                    // Private lookup must throw synchronously, even for a suspended method kind.
                    try { read(object); returned++; }
                    catch (error) { if (error instanceof TypeError) rejected++; }
                }
            }
            console.log(rejected, returned, argumentsEvaluated, bodiesEntered);
            console.log(reader.sync(C), await reader.asynchronous(C), reader.generator(C).next().value,
                (await reader.asyncGenerator(C).next()).value);
            console.log(argumentsEvaluated, bodiesEntered);
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string BrandsExpected = "48 0 0 0\n5 5 5 5\n4 4\n";

    public const string ArgumentsSource = """
        let order = "", receivers = 0;
        function argument() { order += "A"; return 5; }
        function spread() { order += "S"; return [6, 7]; }
        function receiver(value: any) { order += "R"; receivers++; return value; }
        class C {
            static #sync(first: any = 2, optional?: any, ...rest: any[]) {
                return [this === C, first, optional === undefined, rest.join(","), arguments.length].join("|");
            }
            static async #async(first: any = 2, optional?: any, ...rest: any[]) {
                await Promise.resolve(0);
                return [this === C, first, optional === undefined, rest.join(",")].join("|");
            }
            static *#generator(first: any = 2, optional?: any, ...rest: any[]) {
                yield [this === C, first, optional === undefined, rest.join(",")].join("|");
            }
            static async *#asyncGenerator(first: any = 2, optional?: any, ...rest: any[]) {
                await Promise.resolve(0);
                yield [this === C, first, optional === undefined, rest.join(",")].join("|");
            }
            static #ordered(first: any, ...rest: any[]) {
                order += "B";
                return [this === C, first, rest.join(","), arguments.length].join("|");
            }
            static Reader = class {
                static ordered(object: typeof C) { return object.#ordered(argument(), ...spread()); }
                static omitted(object: typeof C) {
                    return [object.#sync(), object.#async(), object.#generator(), object.#asyncGenerator()];
                }
                static supplied(object: typeof C) {
                    return [object.#sync(5, undefined, ...[6, 7]), object.#async(5, undefined, ...[6, 7]),
                        object.#generator(5, undefined, ...[6, 7]), object.#asyncGenerator(5, undefined, ...[6, 7])];
                }
            };
        }
        async function run() {
            const reader: any = C.Reader;
            const omitted: any = reader.omitted(C), supplied: any = reader.supplied(C);
            console.log(omitted[0], await omitted[1], omitted[2].next().value, (await omitted[3].next()).value);
            console.log(supplied[0], await supplied[1], supplied[2].next().value, (await supplied[3].next()).value);
            console.log(reader.ordered(receiver(C)), order, receivers);
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string ArgumentsExpected = "true|2|true||0 true|2|true| true|2|true| true|2|true|\n"
        + "true|5|true|6,7|4 true|5|true|6,7 true|5|true|6,7 true|5|true|6,7\n"
        + "true|5|6,7|3 RASB 1\n";

    public const string OwnersSource = """
        class Outer {
            static #read() { return 11; }
            static declaration() {
                class Reader { static read() { return Outer.#read(); } }
                return Reader;
            }
            static expression() { return class { static read(object: typeof Outer) { return object.#read(); } }; }
        }
        function make() {
            return class Named {
                static #read() { return 21; }
                static read() { return this.#read(); }
                static Nested = class { static read(object: typeof Named) { return object.#read(); } };
            };
        }
        const declared: any = Outer.declaration(), expression: any = Outer.expression();
        console.log(declared.read(), expression.read(Outer));
        const first: any = make(), second: any = make();
        console.log(first.read.call(first), second.read.call(second), first.Nested.read(first), second.Nested.read(second));
        let rejected = 0;
        for (const read of [() => expression.read(expression),
            () => first.read.call(second), () => second.read.call(first), () => first.Nested.read(second)]) {
            try { read(); } catch (error) { if (error instanceof TypeError) rejected++; }
        }
        console.log(rejected, first.read.call(first), second.read.call(second));
        """;

    public const string OwnersExpected = "11 11\n21 21 21 21\n4 21 21\n";

    public const string GenericSource = """
        class Box<T> {
            static #read(first: any = 2, ...rest: any[]) {
                return [this === Box, first, rest.join(","), arguments.length].join("|");
            }
            static read() { return this.#read(...[3, 4, 5]); }
        }
        class Child<T> extends Box<T> {}
        class Other<T> { static #read() { return 9; } }
        const constructor: any = Box;
        const first = new Box<number>(), second = new Box<string>();
        console.log(constructor.read.call(Box), constructor.read.call(constructor), first instanceof Box, second instanceof Box);
        let rejected = 0;
        for (const object of [Child, Other, {}]) {
            try { constructor.read.call(object); } catch (error) { if (error instanceof TypeError) rejected++; }
        }
        console.log(rejected, constructor.read.call(Box));
        """;

    public const string GenericExpected = "true|3|4,5|3 true|3|4,5|3 true true\n3 true|3|4,5|3\n";

    public const string SuspendedSource = """
        let release: any;
        const gate = new Promise<any>((resolve: any) => { release = resolve; });
        class C {
            static async #read(wait: any, label: any, ...rest: any[]) {
                await wait;
                return [this === C, label, rest.join(",")].join("|");
            }
            static async *#values(wait: any, label: any, ...rest: any[]) {
                await wait;
                yield [this === C, label, rest.join(",")].join("|");
                await Promise.resolve(0);
                yield this === C;
            }
            static async invoke(wait: any, label: any) {
                return await this.#read(wait, await Promise.resolve(label), ...[4, 5]);
            }
            static async iterate(wait: any, label: any) {
                return this.#values(wait, await Promise.resolve(label), ...[6, 7]);
            }
        }
        async function run() {
            const invoke: any = C.invoke, iterate: any = C.iterate;
            const left = invoke.call(await Promise.resolve(C), gate, "left"), right = invoke.call(await Promise.resolve(C), gate, "right");
            const first: any = await iterate.call(await Promise.resolve(C), gate, "first"), second: any = await iterate.call(await Promise.resolve(C), gate, "second");
            const nextFirst = first.next(), nextSecond = second.next();
            release(0);
            console.log(await left, await right);
            console.log((await nextFirst).value, (await nextSecond).value);
            console.log((await first.next()).value, (await second.next()).value);
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string SuspendedExpected = "true|left|4,5 true|right|4,5\n"
        + "true|first|6,7 true|second|6,7\ntrue true\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "42\ntrue\n"];
        yield return ["brands", BrandsSource, BrandsExpected];
        yield return ["arguments", ArgumentsSource, ArgumentsExpected];
        yield return ["owners", OwnersSource, OwnersExpected];
        yield return ["generic", GenericSource, GenericExpected];
        yield return ["suspended", SuspendedSource, SuspendedExpected];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, ModeData]
    public void StaticPrivateMethodCalledThroughOwnInstanceThrowsTypeError(ExecutionMode mode)
    {
        const string source = """
            class C {
                static #m() { return 1; }
                static Reader = class {
                    static read(object: typeof C) { return object.#m(); }
                };
            }
            const reader: any = C.Reader;
            console.log(reader.read(C));
            try { reader.read(new C()); }
            catch (error) {
                const caught: any = error;
                console.log(caught.name, caught instanceof TypeError);
            }
            """;
        Assert.Equal("1\nTypeError true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PrivateCallsRejectBeforeAnAwaitedArgument(ExecutionMode mode)
    {
        const string source = """
            let effects = 0;
            class C {
                static async #read(value: any, next: any) { return value; }
                static async read() { return this.#read((effects++, 1), await Promise.resolve(2)); }
            }
            class Other {}
            async function run() {
                const read: any = C.read;
                try { await read.call(Other); }
                catch (error) { console.log(error instanceof TypeError, effects); }
            }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """;
        Assert.Equal("true 0\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void InstancePrivateCallsRejectBeforeArgumentsAndRetainDerivedReceivers(ExecutionMode mode)
    {
        const string source = """
            let effects = 0, receivers = 0;
            function receiver(value: any) { receivers++; return value; }
            class C {
                #m(value: any) { return [this instanceof C, value].join("|"); }
                read(object: any) { return receiver(object).#m(++effects); }
            }
            class Child extends C {}
            class Other { #m(value: any) { return value; } }
            const reader: any = new C();
            let rejected = 0;
            for (const object of [new Other(), {}, undefined, 1, "value", false, Symbol("value"), 1n]) {
                try { reader.read(object); }
                catch (error) { if (error instanceof TypeError) rejected++; }
            }
            console.log(rejected, effects, receivers);
            console.log(reader.read(reader), reader.read(new Child()), effects, receivers);
            """;
        Assert.Equal("8 0 8\ntrue|1 true|2 2 10\n", TestHarness.Run(source, mode));
    }

    public const string NullInstanceReceiverSource = """
        let effects = 0;
        class C {
            #m(value: any) { return value; }
            read(object: any) { return object.#m(++effects); }
        }
        const reader: any = new C();
        try { reader.read(null); }
        catch (error: any) { console.log(error.name, error instanceof TypeError, effects); }
        """;

    public const string AsyncNullInstanceReceiverSource = """
        let effects = 0, receivers = 0;
        const marker = {};
        function receiver(value: any) {
            receivers++;
            if (value === marker) throw marker;
            return value;
        }
        class C {
            #m(value: any) { return [this instanceof C, value].join("|"); }
            async read(object: any) {
                await Promise.resolve(0);
                return receiver(object).#m(++effects);
            }
        }
        class Child extends C {}
        async function run() {
            const reader: any = new C();
            try { await reader.read(null); }
            catch (error: any) { console.log(error.name, error instanceof TypeError, effects, receivers); }
            console.log(await reader.read(new Child()), effects, receivers);
            try { await reader.read(marker); }
            catch (error: any) { console.log(error === marker, effects, receivers); }
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public const string GeneratorNullInstanceReceiverSource = """
        let effects = 0, receivers = 0;
        function receiver(value: any) { receivers++; return value; }
        class C {
            #m(value: any) { return value; }
            *read(object: any) {
                yield 0;
                return receiver(object).#m(++effects);
            }
        }
        class Child extends C {}
        const reader: any = new C();
        const invalid = reader.read(null);
        console.log(invalid.next().value);
        try { invalid.next(); }
        catch (error: any) { console.log(error.name, error instanceof TypeError, effects, receivers); }
        const valid = reader.read(new Child());
        valid.next();
        const completed = valid.next();
        console.log(completed.value, completed.done, effects, receivers);
        """;

    public const string AsyncGeneratorNullInstanceReceiverSource = """
        let effects = 0, receivers = 0;
        function receiver(value: any) { receivers++; return value; }
        function pause(): Promise<void> {
            return new Promise<void>(resolve => { setTimeout(() => resolve(), 1); });
        }
        class C {
            #m(value: any) { return [this instanceof C, value].join("|"); }
            async *read(object: any): AsyncGenerator<number, string, unknown> {
                yield 0;
                await pause();
                return receiver(object).#m(++effects);
            }
        }
        class Child extends C {}
        async function run(): Promise<void> {
            const reader: any = new C();
            const invalid = reader.read(null);
            console.log((await invalid.next()).value);
            try { await invalid.next(); }
            catch (error: any) { console.log(error.name, error instanceof TypeError, effects, receivers); }
            const valid = reader.read(new Child());
            await valid.next();
            const completed = await valid.next();
            console.log(completed.value, completed.done, effects, receivers);
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    [Theory, ModeData]
    public void NullInstanceReceiversRejectWithTypeErrorBeforeArguments(ExecutionMode mode)
    {
        Assert.Equal("TypeError true 0\n", TestHarness.Run(NullInstanceReceiverSource, mode));
    }

    [Theory, ModeData]
    public void AsyncNullInstanceReceiversEvaluateReceiverOnceBeforeArguments(ExecutionMode mode)
    {
        Assert.Equal("TypeError true 0 1\ntrue|1 1 2\ntrue 1 3\n",
            TestHarness.Run(AsyncNullInstanceReceiverSource, mode));
    }

    [Theory, ModeData]
    public void GeneratorNullInstanceReceiversRejectBeforeArgumentsAfterYield(ExecutionMode mode)
    {
        Assert.Equal("0\nTypeError true 0 1\n1 true 1 2\n",
            TestHarness.Run(GeneratorNullInstanceReceiverSource, mode));
    }

    [Theory, ModeData]
    public void AsyncGeneratorNullInstanceReceiversRejectBeforeArgumentsAfterYieldAndAwait(ExecutionMode mode)
    {
        Assert.Equal("0\nTypeError true 0 1\ntrue|1 true 1 2\n",
            TestHarness.Run(AsyncGeneratorNullInstanceReceiverSource, mode));
    }

    public static IEnumerable<object[]> TypedNullInstanceReceiverCases()
    {
        const string dynamicCaller = "const reader: any = new C();";
        const string typedCaller = "const reader = new C();";
        yield return ["typed-issue", NullInstanceReceiverSource.Replace(dynamicCaller, typedCaller, StringComparison.Ordinal),
            "TypeError true 0\n"];
        yield return ["typed-async-reader", AsyncNullInstanceReceiverSource.Replace(dynamicCaller, typedCaller, StringComparison.Ordinal),
            "TypeError true 0 1\ntrue|1 1 2\ntrue 1 3\n"];
        yield return ["typed-generator-reader", GeneratorNullInstanceReceiverSource.Replace(dynamicCaller, typedCaller, StringComparison.Ordinal),
            "0\nTypeError true 0 1\n1 true 1 2\n"];
        yield return ["typed-async-generator-reader", AsyncGeneratorNullInstanceReceiverSource.Replace(dynamicCaller, typedCaller, StringComparison.Ordinal),
            "0\nTypeError true 0 1\ntrue|1 true 1 2\n"];
    }

    public static IEnumerable<object[]> TypedNullInstanceReceiverModeCases() => TypedNullInstanceReceiverCases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(TypedNullInstanceReceiverModeCases))]
    public void TypedNullInstanceReceiversRejectBeforeArgumentsAcrossMethodKinds(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedInstancePrivateCallsRetainCapturedScopeAndSuperReceiver()
    {
        const string source = """
            function make(prefix: string) {
                class Base { read() { return "base"; } }
                class C extends Base {
                    #m() { return prefix + ":" + super.read(); }
                    read() { return this.#m(); }
                }
                return new C();
            }
            console.log(make("outer").read());
            """;
        Assert.Equal("outer:base\n", TestHarness.RunInterpreted(source));
    }

    [Theory, ModeData]
    public void AwaitedPrivateReceiversRejectBeforeArgumentEffects(ExecutionMode mode)
    {
        const string source = """
            let effects = 0;
            class C {
                static #m(value: any) { return value; }
                static async read(object: typeof C) {
                    return (await Promise.resolve(object)).#m((effects++, await Promise.resolve(7)));
                }
            }
            class Other {}
            async function run() {
                const reader: any = C;
                try { await reader.read(Other); }
                catch (error) { console.log(error instanceof TypeError, effects); }
                console.log(await reader.read(C), effects);
            }
            run().catch((error: any) => console.log("unexpected", error.name, error.message));
            """;
        Assert.Equal("true 0\n7 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void BrandedNoncallableFieldsStillEvaluateArgumentsBeforeRejecting(ExecutionMode mode)
    {
        const string source = """
            let effects = 0;
            class C {
                #field: any = 3;
                static #staticField: any = 4;
                fail() { return this.#field(++effects); }
                static fail() { return this.#staticField(++effects); }
            }
            try { new C().fail(); }
            catch (error) { console.log(error instanceof TypeError, effects); }
            try { C.fail(); }
            catch (error) { console.log(error instanceof TypeError, effects); }
            """;
        Assert.Equal("true 1\ntrue 2\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void HostedCompiledPrivateCallsRejectProxyConstructorReceivers()
    {
        // Proxy currently needs the hosted runtime, so this control is separate
        // from the isolated --standalone sources.
        const string source = """
            class C {
                static #sync() { return 1; }
                static async #async() { return 1; }
                static *#generator() { yield 1; }
                static async *#asyncGenerator() { yield 1; }
                static sync() { return this.#sync(); }
                static asynchronous() { return this.#async(); }
                static generator() { return this.#generator(); }
                static asyncGenerator() { return this.#asyncGenerator(); }
            }
            const constructor: any = C, proxy: any = new Proxy(constructor, {});
            let rejected = 0;
            for (const read of [constructor.sync, constructor.asynchronous, constructor.generator, constructor.asyncGenerator]) {
                try { read.call(proxy); }
                catch (error) { if (error instanceof TypeError) rejected++; }
            }
            console.log(rejected);
            """;
        Assert.Equal("4\n", TestHarness.RunCompiled(source));
    }

    [Theory, MemberData(nameof(ParityCases))]
    public void StaticPrivateCallsCheckTheirActualReceiverBeforeArguments(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void SameNamedModuleOwnersRetainDistinctStaticPrivateBrands(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["left.ts"] = "export class Box<T> { static #read() { return 31; } static read() { return this.#read(); } }",
            ["right.ts"] = "export class Box<T> { static #read() { return 32; } static read() { return this.#read(); } }",
            ["main.ts"] = """
                import { Box as Left } from './left';
                import { Box as Right } from './right';
                const left: any = Left, right: any = Right;
                console.log(left.read.call(left), right.read.call(right));
                try { left.read.call(right); } catch (error) { console.log(error instanceof TypeError); }
                try { right.read.call(left); } catch (error) { console.log(error instanceof TypeError); }
                console.log(left.read.call(left), right.read.call(right));
                """
        };
        Assert.Equal("31 32\ntrue\ntrue\n31 32\n", TestHarness.RunModules(files, "main.ts", mode));
        if (mode == ExecutionMode.Compiled)
            Assert.Empty(TestHarness.CompileModulesAndVerifyOnly(files, "main.ts"));
    }
}
