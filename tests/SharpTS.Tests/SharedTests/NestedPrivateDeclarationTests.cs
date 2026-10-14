using SharpTS.Tests.Infrastructure;
using SharpTS.Parsing;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NestedPrivateDeclarationTests
{
    public const string IssueSource = """
        class Outer {
            static #read(): number { return 42; }
            make() {
                class Inner { read() { return Outer.#read(); } }
                return new Inner();
            }
        }
        console.log(new Outer().make().read());
        """;

    public const string GenericExpressionIssueSource = """
        const Outer: any = class Outer<T> {
            static #read(): number { return 42; }
            make() {
                class Inner { read() { return Outer.#read(); } }
                return new Inner();
            }
        };
        console.log(new Outer().make().read());
        """;

    public const string MetadataControlSource = """
        @Namespace("NestedOwners")
        const Outer = class {
            make() {
                class Inner { read() { return 42; } }
                @Obsolete("nested declaration metadata")
                class Decorated { read() { return 43; } }
                return [new Inner(), new Decorated()];
            }
        };
        const values: any = new Outer().make();
        console.log(values[0].read(), values[1].read());
        """;

    [Theory, ModeData]
    public void NestedDeclarationCallsEnclosingStaticPrivateMethod(ExecutionMode mode)
        => Assert.Equal("42\n", TestHarness.Run(IssueSource, mode));

    [Theory, ModeData]
    public void NestedDeclarationFindsGenericExpressionOwner(ExecutionMode mode)
        => Assert.Equal("42\n", TestHarness.Run(GenericExpressionIssueSource, mode));

    [Theory, ModeData]
    public void NestedDeclarationReadsWritesCallsAndProbesBothPrivateFacets(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                #value = 40;
                #read() { return this.#value + 2; }
                static #staticValue = 7;
                static #staticRead() { return Outer.#staticValue; }
                make() {
                    class Inner {
                        read(o: Outer) { return o.#value; }
                        write(o: Outer) { o.#value = 41; }
                        call(o: Outer) { return o.#read(); }
                        has(o: any) { return #value in o; }
                        readStatic() { return Outer.#staticValue; }
                        writeStatic() { Outer.#staticValue = 8; }
                        callStatic() { return Outer.#staticRead(); }
                    }
                    return new Inner();
                }
            }
            const outer = new Outer(), inner = outer.make();
            console.log(inner.read(outer), inner.call(outer), inner.has(outer), inner.has(inner));
            inner.write(outer);
            console.log(inner.read(outer), inner.call(outer));
            console.log(inner.readStatic(), inner.callStatic());
            inner.writeStatic();
            console.log(inner.readStatic(), inner.callStatic());
            """;
        Assert.Equal("40 42 true false\n41 43\n7 7\n8 8\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ConstructorInitializerAccessorAndArrowKeepTheOwnerChain(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                static #read(): number { return 42; }
                make() {
                    class Inner {
                        value = Outer.#read();
                        constructor() { console.log(Outer.#read()); }
                        get read() { return Outer.#read(); }
                        arrow() { return () => Outer.#read(); }
                    }
                    return new Inner();
                }
            }
            const inner = new Outer().make();
            console.log(inner.value, inner.read, inner.arrow()());
            """;
        Assert.Equal("42\n42 42 42\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AsyncAndGeneratorBodiesKeepTheOwnerChain(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                static #read(): number { return 42; }
                make() {
                    class Inner {
                        async read() { return Outer.#read(); }
                        *values() { yield Outer.#read(); }
                        async *asyncValues() { yield Outer.#read(); }
                    }
                    return new Inner();
                }
            }
            async function main() {
                const inner = new Outer().make();
                console.log(await inner.read());
                console.log(inner.values().next().value);
                console.log((await inner.asyncValues().next()).value);
            }
            main();
            """;
        Assert.Equal("42\n42\n42\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ClassExpressionInsideDeclarationsFindsTheOuterDeclaration(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                static #read(): number { return 42; }
                make() {
                    class Middle {
                        make() { return class { read() { return Outer.#read(); } }; }
                    }
                    return new Middle().make();
                }
            }
            const Inner = new Outer().make();
            console.log(new Inner().read());
            """;
        Assert.Equal("42\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void TheNearestSameKindNameKeepsItsOwnBrand(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                #value = "outer";
                #read() { return this.#value; }
                make() {
                    class Inner {
                        #value = "inner";
                        #read() { return this.#value; }
                        read() { return this.#read(); }
                        has(o: any) { return #value in o; }
                    }
                    return new Inner();
                }
            }
            const outer = new Outer(), inner = outer.make();
            console.log(inner.read(), inner.has(inner), inner.has(outer));
            """;
        Assert.Equal("inner true false\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void SameNamedOwnersInDifferentModulesStayDistinct(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["left.ts"] = "export class Outer { static #read() { return 41; } make() { class Inner { read() { return Outer.#read(); } } return new Inner(); } }",
            ["right.ts"] = "export class Outer { static #read() { return 42; } make() { class Inner { read() { return Outer.#read(); } } return new Inner(); } }",
            ["main.ts"] = """
                import { Outer as Left } from './left';
                import { Outer as Right } from './right';
                console.log(new Left().make().read(), new Right().make().read());
                """
        };
        Assert.Equal("41 42\n", TestHarness.RunModules(files, "main.ts", mode));
    }

    [Theory, ModeData]
    public void NamespacedNestedBodiesUseTheQualifiedOwnerIdentity(ExecutionMode mode)
    {
        const string source = """
            @Namespace("Owners")
            class Outer {
                static #read(): number { return 42; }
                make() {
                    class Inner {
                        read() { return Outer.#read(); }
                        async readAsync() { return Outer.#read(); }
                        *values() { yield Outer.#read(); }
                    }
                    return new Inner();
                }
            }
            async function main() {
                const inner = new Outer().make();
                console.log(inner.read(), await inner.readAsync(), inner.values().next().value);
            }
            main();
            """;
        Assert.Equal("42 42 42\n", TestHarness.Run(source, mode, DecoratorMode.Legacy));
    }

    [Theory, ModeData]
    public void RepeatedOuterExpressionsKeepCapturedOwnersAndFreshInnerDefinitions(ExecutionMode mode)
    {
        const string source = """
            function make(value: number) {
                return class Outer {
                    static #value = value;
                    static #read() { return Outer.#value; }
                    make() {
                        class Middle {
                            make() {
                                class Inner {
                                    read() { return Outer.#read(); }
                                    value() { return Outer.#value; }
                                    write(value: number) { Outer.#value = value; }
                                    has(value: any) { return #read in value; }
                                }
                                return Inner;
                            }
                        }
                        return new Middle().make();
                    }
                };
            }
            const First = make(41), Second = make(42);
            const A = new First().make(), Again = new First().make(), B = new Second().make();
            const a = new A(), b = new B();
            console.log(A !== Again, a.read(), b.read(), a.value(), b.value());
            console.log(a.has(First), a.has(Second), b.has(First), b.has(Second));
            a.write(43);
            console.log(a.read(), new Again().read(), b.read());
            """;
        Assert.Equal("true 41 42 41 42\ntrue false false true\n43 43 42\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NestedExpressionsUseTheCapturedOuterPrivateOwner(ExecutionMode mode)
    {
        const string source = """
            function make(value: number) {
                return class Outer {
                    #value = value;
                    #read() { return this.#value; }
                    make() {
                        return class Inner {
                            read(value: Outer) { return value.#read(); }
                            value(value: Outer) { return value.#value; }
                            has(value: any) { return #read in value; }
                        };
                    }
                };
            }
            const First = make(41), Second = make(42);
            const first = new First(), second = new Second();
            const A = first.make(), B = second.make();
            const a = new A(), b = new B();
            console.log(a.read(first), b.read(second), a.value(first), b.value(second));
            console.log(a.has(first), a.has(second), b.has(first), b.has(second));
            """;
        Assert.Equal("41 42 41 42\ntrue false false true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ConstructorTypedParameterAndLocalKeepTheirValueBinding(ExecutionMode mode)
    {
        const string source = """
            function make() {
                return class Outer {
                    make() {
                        class Inner {
                            read(Outer: typeof Outer) { return Outer; }
                            local(value: typeof Outer) { const Outer = value; return Outer; }
                            arrow(Outer: typeof Outer) { return () => Outer; }
                            localArrow(value: typeof Outer) { const Outer = value; return () => Outer; }
                            nestedArrow(Outer: typeof Outer) { return () => () => Outer; }
                            capture(value: typeof Outer) {
                                const Outer = value;
                                return class Reader { read() { return Outer; } };
                            }
                        }
                        return new Inner();
                    }
                };
            }
            const First = make();
            const Second: any = make();
            const inner = new First().make();
            const Reader = inner.capture(Second);
            console.log(inner.read(Second) === Second, inner.local(Second) === Second, new Reader().read() === Second,
                inner.arrow(Second)() === Second, inner.localArrow(Second)() === Second, inner.nestedArrow(Second)()() === Second);
            """;
        Assert.Equal("true true true true true true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NamespacedNestedDeclarationKeepsItsDeclarationHandling(ExecutionMode mode)
    {
        const string source = """
            @Namespace("NestedOwners")
            const Outer = class {
                make() {
                    class Inner { read() { return 42; } }
                    return new Inner();
                }
            };
            console.log(new Outer().make().read());
            """;
        Assert.Equal("42\n", TestHarness.Run(source, mode, DecoratorMode.Legacy));
    }

    [Theory, ModeData]
    public void NestedDeclarationWithSupportedArrayHeritageKeepsItsOwner(ExecutionMode mode)
    {
        const string source = """
            const Outer = class Outer {
                static #read() { return 42; }
                make() {
                    class Inner extends Array {
                        read() { return Outer.#read(); }
                    }
                    return new Inner();
                }
            };
            const inner: any = new Outer().make();
            inner.push(1, 2);
            console.log(inner.read(), inner.length, inner[0], inner[1], Array.isArray(inner));
            """;
        Assert.Equal("42 2 1 2 true\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void NestedDeclarationKeepsUnsupportedBuiltInHeritageDiagnostic()
    {
        const string source = """
            const Outer = class {
                make() { class Inner extends Map {} return Inner; }
            };
            console.log(new Outer().make());
            """;
        var error = Assert.ThrowsAny<Exception>(() => TestHarness.RunCompiled(source));
        Assert.Contains("cannot extend built-in 'Map'", error.Message);
    }

    [Theory, ModeData]
    public void ArrowsCreateFreshDeclarationsWithTheirLexicalDefinition(ExecutionMode mode)
    {
        const string source = """
            function make() {
                return class {
                    create() {
                        return () => {
                            class Inner {
                                #inner = 13;
                                read(value: any) { return () => value.#inner; }
                            }
                            return Inner;
                        };
                    }
                };
            }
            const First = make(), Second = make();
            const Left = new First().create()(), Right = new Second().create()();
            const left: any = new Left(), right: any = new Right();
            console.log(left.read(left)(), right.read(right)(), Left === Right);
            try { left.read(right)(); } catch (error) { console.log(error instanceof TypeError); }
            """;
        Assert.Equal("13 13 false\ntrue\n", TestHarness.Run(source, mode));
    }

}
