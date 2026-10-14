using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrivateCallableFieldTests
{
    public const string IssueSource = """
        class C {
            #f = () => 42;
            static #g = () => 43;
            read() { return this.#f(); }
            static read() { return this.#g(); }
        }
        console.log(new C().read(), C.read());
        """;

    public const string ReceiverSource = """
        class C {
            #value = 40;
            #arrow = (value: number) => this.#value + value;
            #function = function(this: any, value: number) { return this.#value + value; };
            static #staticFunction = function(this: any, value: number) { return [this === C, value].join("|"); };
            read() { return [this.#arrow(1), this.#function(2)].join("|"); }
            static read() { return this.#staticFunction(43); }
        }
        console.log(new C().read(), C.read());
        """;

    public const string EffectsSource = """
        let effects = 0, receivers = 0;
        function receiver(value: any) { receivers++; return value; }
        class C {
            #f: any = () => 1;
            #bad: any = 1;
            static #s = (value: number) => value;
            replace() { this.#f = () => 2; return 0; }
            read(value: any) { return receiver(value).#f(); }
            change() { return this.#f(this.replace()); }
            fail() { return this.#bad(++effects); }
            static fail() { return this.#s(++effects); }
        }
        const value = new C();
        console.log(value.change(), value.read(value), receivers);
        try { value.fail(); } catch (error) { console.log(error instanceof TypeError, effects); }
        class Other {}
        const read: any = C.fail;
        try { read.call(Other); } catch (error) { console.log(error instanceof TypeError, effects); }
        console.log(C.fail(), effects);
        """;

    public const string OwnersSource = """
        class Box<T> {
            #f = (value: number) => value + 1;
            static #s = (value: number) => value + 2;
            read() { return this.#f(41); }
            static readStatic() { return Box.#s(41); }
        }
        function make() {
            return class Named {
                #f = () => 51;
                static #s = () => 52;
                read() { return this.#f(); }
                static read() { return this.#s(); }
            };
        }
        const First: any = make(), Second: any = make();
        console.log(new Box<number>().read(), new Box<string>().read(), Box.readStatic());
        console.log(new First().read(), First.read(), new Second().read(), Second.read());
        """;

    public const string AsyncSource = """
        async function answer() { return 45; }
        async function dynamicAnswer() { return 46; }
        class C {
            #f = (value: number) => value + 1;
            #promise = answer;
            #dynamic: any = dynamicAnswer;
            async read() { return (await Promise.resolve(this)).#f(await Promise.resolve(41)); }
            async readPromise() { return this.#promise(); }
            async readDynamic() { return this.#dynamic(); }
            async readSync() { return this.#f(48); }
            *values() { yield this.#f(42); }
            async *asyncValues() { yield this.#f(await Promise.resolve(43)); }
        }
        async function run() {
            const value = new C();
            console.log(await value.read(), value.values().next().value,
                (await value.asyncValues().next()).value);
            console.log(await value.readPromise(), await value.readDynamic(), await value.readSync());
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "42 43\n"];
        yield return ["receivers", ReceiverSource, "41|42 true|43\n"];
        yield return ["effects", EffectsSource, "1 2 1\ntrue 1\ntrue 1\n2 2\n"];
        yield return ["owners", OwnersSource, "42 42 43\n51 52 51 52\n"];
        yield return ["async", AsyncSource, "42 43 44\n45 46 49\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void CallableFieldsRetainTheirOwnerAndReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NearestPrivateDeclarationSelectsFieldOrMethod(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                #f() { return 1; }
                Inner = class {
                    #f = () => 2;
                    read() { return this.#f(); }
                };
            }
            class Other {
                #f = () => 3;
                Inner = class {
                    #f() { return 4; }
                    read() { return this.#f(); }
                };
            }
            const First = new Outer().Inner, Second = new Other().Inner;
            console.log(new First().read(), new Second().read());
            """;
        Assert.Equal("2 4\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void InlineCallableSignaturesWorkInDeclarationsAndExpressions(ExecutionMode mode)
    {
        const string source = """
            class Declaration {
                #f: (value: number) => number = (value: number) => value + 1;
                read() { return this.#f(41); }
            }
            const Expression = class {
                #f: (value: number) => number = (value: number) => value + 2;
                read() { return this.#f(41); }
            };
            console.log(new Declaration().read(), new Expression().read());
            """;
        Assert.Equal("42 43\n", TestHarness.Run(source, mode));
    }

    // Interpreter async returns currently skip thenable adoption (#2146).
    [Theory, CompiledOnlyData]
    public void AsyncReturnAdoptsAnyTypedFieldThenable(ExecutionMode mode)
    {
        const string source = """
            class C {
                #f: any = () => ({ then(resolve: any) { resolve(47); } });
                async read() { return this.#f(); }
            }
            async function run() { console.log(await new C().read()); }
            run();
            """;
        Assert.Equal("47\n", TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("class C { #f: number = 1; read() { return this.#f(); } }", "TS2349")]
    [InlineData("type Fn = (value: number) => number; class C { #f: Fn = (value: number) => value; read() { return this.#f('bad'); } }", "TS2345")]
    [InlineData("class C { #f: (value: number) => number = (value: number) => value; read() { return this.#f('bad'); } }", "TS2345")]
    [InlineData("const C = class { #f: (value: number) => number = (value: number) => value; read() { return this.#f('bad'); } };", "TS2345")]
    public void CallableFieldsStillCheckTheirValueAndArguments(string source, string code)
    {
        var error = Assert.Throws<TypeCheckException>(() => TestHarness.RunInterpreted(source));
        Assert.Equal(code, error.Diagnostic.TsCode);
    }
}
