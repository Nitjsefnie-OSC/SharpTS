using SharpTS.Execution;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.InterpreterTests;

public class NestedPrivateNameTests
{
    [Fact]
    public void NestedClassReadsWritesAndCallsEnclosingPrivateNames()
    {
        const string source = """
            class Outer {
                #outer = "test262";
                #method() { return this.#outer; }
                Inner = class {
                    #inner = 42;
                    read(o: any) { return o.#outer; }
                    write(o: any) { o.#outer = "updated"; }
                    call(o: any) { return o.#method(); }
                    has(o: any) { return #outer in o; }
                };
            }
            const outer = new Outer();
            const inner = new outer.Inner();
            console.log(inner.read(outer), inner.call(outer), inner.has(outer));
            inner.write(outer);
            console.log(inner.read(outer), inner.call(outer));
            """;
        Assert.Equal("test262 test262 true\nupdated updated\n", RunJavaScript(source));
    }

    [Fact]
    public void NestedPrivateNameShadowsEnclosingNameAndKeepsBrandChecks()
    {
        const string source = """
            class Outer {
                #value = "outer";
                Inner = class {
                    #value = "inner";
                    read(o: any) { return o.#value; }
                    has(o: any) { return #value in o; }
                };
            }
            const outer = new Outer();
            const inner = new outer.Inner();
            console.log(inner.read(inner), inner.has(inner), inner.has(outer));
            try { inner.read(outer); } catch (error) { console.log(error instanceof TypeError); }
            """;
        Assert.Equal("inner true false\ntrue\n", RunJavaScript(source));
    }

    [Fact]
    public void NestedSubclassCapturesEnclosingReceiver()
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
        Assert.Equal("42\n", RunJavaScript(source));
    }

    [Fact]
    public void NestedClassKeepsOuterStaticNamesAndRejectsShadowedReceivers()
    {
        const string source = """
            class Outer {
                static #value = 42;
                static #method() { return this.#value; }
                Inner = class {
                    #inner;
                    read() { return Outer.#value; }
                    write() { Outer.#value = 43; }
                    call() { return Outer.#method(); }
                };
                Shadow = class {
                    #value;
                    #method() {}
                    read() { return Outer.#value; }
                    write() { Outer.#value = 0; }
                    call() { return Outer.#method(); }
                };
            }
            const outer = new Outer();
            const inner = new outer.Inner();
            console.log(inner.read(), inner.call());
            inner.write();
            console.log(inner.read(), inner.call());
            const shadow = new outer.Shadow();
            try { shadow.read(); } catch (error) { console.log(error instanceof TypeError); }
            try { shadow.write(); } catch (error) { console.log(error instanceof TypeError); }
            try { shadow.call(); } catch (error) { console.log(error instanceof TypeError); }
            console.log(inner.read());
            """;
        Assert.Equal("42 42\n43 43\ntrue\ntrue\ntrue\n43\n", RunJavaScript(source));
    }

    // The checker currently rejects enclosing private names (#2001). Test262
    // covers both engines without checking; these exercise the interpreter API.
    private static string RunJavaScript(string source)
    {
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        using var output = new StringWriter();
        using var interpreter = new Interpreter(stdout: output, stderr: TextWriter.Null);
        interpreter.Interpret(statements, new TypeMap());
        Assert.Null(interpreter.LastUncaughtError);
        return output.ToString().Replace("\r\n", "\n");
    }
}
