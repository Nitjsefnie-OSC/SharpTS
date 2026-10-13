using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class LexicalPrivateNameTests
{
    [Theory, ModeData]
    public void NestedSubclassReadsCapturedEnclosingPrivateField(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                #value = 42;
                make() {
                    const outer = this;
                    return class Inner extends Outer {
                        #inner = 1;
                        read() { return outer.#value; }
                    };
                }
            }
            const Inner = new Outer().make();
            console.log(new Inner().read());
            """;
        Assert.Equal("42\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NestedFieldClassReadsWritesCallsAndProbesEnclosingNames(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                #outer: number = 42;
                #method(value: number): number { return this.#outer + value; }
                static #static: number = 7;
                static #read(): number { return Outer.#static; }
                Inner = class {
                    #inner = 1;
                    read(o: Outer) { return o.#outer; }
                    write(o: Outer) { o.#outer = 43; }
                    call(o: Outer) { return o.#method(2); }
                    has(o: any) { return #outer in o; }
                    readStatic() { return Outer.#static; }
                    writeStatic() { Outer.#static = 8; }
                    callStatic() { return Outer.#read(); }
                };
            }
            const outer = new Outer(), inner = new outer.Inner();
            console.log(inner.read(outer), inner.call(outer), inner.has(outer), inner.has(inner));
            inner.write(outer);
            console.log(inner.read(outer), inner.call(outer));
            console.log(inner.readStatic(), inner.callStatic());
            inner.writeStatic();
            console.log(inner.readStatic(), inner.callStatic());
            """;
        Assert.Equal("42 44 true false\n43 45\n7 7\n8 8\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void InnerNameShadowsOuterBrand(ExecutionMode mode)
    {
        const string source = """
            class Outer {
                #value = "outer";
                make() { return class Inner {
                    #value = "inner";
                    read(o: any) { return o.#value; }
                    has(o: any) { return #value in o; }
                }; }
            }
            const outer = new Outer(), Inner = outer.make(), inner = new Inner();
            console.log(inner.read(inner), inner.has(inner), inner.has(outer));
            try { inner.read(outer); } catch (error) { console.log(error instanceof TypeError); }
            """;
        Assert.Equal("inner true false\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NestedDeclarationsFindGenericOuterStaticNames(ExecutionMode mode)
    {
        const string source = """
            class Outer<T> {
                static #value: number = 42;
                static #read(): number { return Outer.#value; }
                make() {
                    class Middle {
                        make() {
                            class Inner {
                                read() { return Outer.#read(); }
                                write() { Outer.#value = 43; }
                            }
                            return new Inner();
                        }
                    }
                    return new Middle().make();
                }
            }
            const inner = new Outer<number>().make();
            console.log(inner.read());
            inner.write();
            console.log(inner.read());
            """;
        Assert.Equal("42\n43\n", TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("o.#value = 'bad';", "TS2322")]
    [InlineData("o.#method('bad');", "TS2345")]
    [InlineData("o.#method();", "TS2554")]
    [InlineData("o.#missing;", "TS2339")]
    public void EnclosingPrivateTypesAndArityRemainChecked(string body, string code)
    {
        string source = "class Outer { #value: number = 1; #method(value: number) { return value; } " +
            "make() { return class Inner { inspect(o: Outer) { " + body + " } }; } }";
        var error = Assert.Throws<TypeCheckException>(() => TestHarness.RunInterpreted(source));
        Assert.Equal(code, error.Diagnostic.TsCode);
    }
}
