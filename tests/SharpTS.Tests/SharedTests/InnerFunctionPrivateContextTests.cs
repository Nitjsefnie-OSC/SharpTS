using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InnerFunctionPrivateContextTests
{
    public const string IssueSource = """
        class C {
            #x: number = 1;
            read() {
                function f(other: C) { return other.#x; }
                return f(this);
            }
        }
        console.log(new C().read());
        """;

    public const string EscapedSource = """
        class C {
            #value = 40;
            #add(value: number) { return this.#value + value; }
            make(offset: number) {
                function read(other: C) { return other.#value + offset; }
                function write(other: any, value: number) { other.#value = value; }
                function call(other: C) { return other.#add(offset); }
                function has(other: any) { return #value in other; }
                return { read, write, call, has };
            }
        }
        const instance = new C(), access = instance.make(2);
        console.log(access.read(instance), access.call(instance), access.has(instance), access.has({}));
        access.write(instance, 43);
        console.log(access.read(instance), access.call(instance));
        try { access.read({} as any); } catch (error) { console.log(error instanceof TypeError); }
        try { access.write({}, 0); } catch (error) { console.log(error instanceof TypeError); }
        try { access.call({} as any); } catch (error) { console.log(error instanceof TypeError); }
        console.log(access.read(instance));
        """;

    public const string StaticSource = """
        class C {
            static #value = 7;
            static #add(value: number) { return C.#value + value; }
            static make() {
                function read() { return C.#value; }
                function write(value: number) { C.#value = value; }
                function call(value: number) { return C.#add(value); }
                function has(value: any) { return #value in value; }
                return { read, write, call, has };
            }
        }
        const access = C.make();
        console.log(access.read(), access.call(2), access.has(C), access.has(new C()));
        access.write(11);
        console.log(access.read(), access.call(3));
        """;

    public const string NestedFunctionsSource = """
        class C {
            #x = 9;
            read() {
                function outer(other: C) {
                    function inner(value: C) { return value.#x; }
                    return inner(other);
                }
                return outer(this);
            }
        }
        console.log(new C().read());
        """;

    public const string DynamicThisSource = """
        class C {
            #value: number;
            constructor(value: number) { this.#value = value; }
            make(offset: number) {
                function read(this: C) { return this.#value + offset; }
                return read;
            }
        }
        const first = new C(10), second = new C(20), read = first.make(2);
        console.log(read.call(second), read.call(first));
        try { read.call({}); } catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string NoncapturingThisSource = """
        class C {
            static marker = 90;
            static read() { return 91; }
            make() {
                function read(this: any) { return this.marker; }
                function write(this: any, value: number) { this.marker = value; }
                function call(this: any) { return this.read(); }
                return { read, write, call };
            }
        }
        const access = new C().make();
        const receiver = { marker: 2, read() { return this.marker + 1; } };
        console.log(access.read.call(receiver), access.call.call(receiver));
        access.write.call(receiver, 5);
        console.log(access.read.call(receiver), access.call.call(receiver), C.marker, C.read());
        """;

    public const string ClassExpressionSource = """
        function make(value: number) {
            return class {
                #value = value;
                reader() {
                    function read(other: any) { return other.#value; }
                    return read;
                }
            };
        }
        const First = make(3), Second = make(7);
        const first = new First(), second = new Second();
        const readFirst = first.reader(), readSecond = second.reader();
        console.log(readFirst(first), readSecond(second));
        try { readFirst(second); } catch (error) { console.log(error instanceof TypeError); }
        try { readSecond(first); } catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string DefaultParameterSource = """
        const C = class {
            #value = 5;
            reader() {
                function read(other: any, value: number = other.#value) { return value; }
                return read;
            }
        };
        const instance = new C(), read = instance.reader();
        console.log(read(instance), read(instance, 9));
        try { read({}); } catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string ShadowedOwnerSource = """
        class Outer {
            #value = "outer";
            #unshadowed = "outer-control";
            make() {
                class Inner {
                    #value = "inner";
                    read(value: any) {
                        function forward(other: any) {
                            function read(nested: any) { return nested.#value; }
                            return read(other);
                        }
                        return forward(value);
                    }
                    readOuter(value: Outer) {
                        function read(other: Outer) { return other.#unshadowed; }
                        return read(value);
                    }
                    has(value: any) {
                        function has(other: any) { return #value in other; }
                        return has(value);
                    }
                }
                return new Inner();
            }
        }
        const outer = new Outer(), inner = outer.make(), other = outer.make();
        console.log(inner.read(inner), inner.readOuter(outer), inner.has(inner), inner.has(outer), inner.has(other));
        try { inner.read(outer); } catch (error) { console.log(error instanceof TypeError); }
        try { inner.read(other); } catch (error) { console.log(error instanceof TypeError); }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "1\n"];
        yield return ["escaped", EscapedSource, "42 42 true false\n45 45\ntrue\ntrue\ntrue\n45\n"];
        yield return ["static", StaticSource, "7 9 true false\n11 14\n"];
        yield return ["nested-functions", NestedFunctionsSource, "9\n"];
        yield return ["dynamic-this", DynamicThisSource, "22 12\ntrue\n"];
        yield return ["noncapturing-this", NoncapturingThisSource, "2 3\n5 6 90 91\n"];
        yield return ["class-expression", ClassExpressionSource, "3 7\ntrue\ntrue\n"];
        yield return ["default-parameter", DefaultParameterSource, "5 9\ntrue\n"];
        yield return ["shadowed-owner", ShadowedOwnerSource, "inner outer-control true false false\ntrue\ntrue\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OrdinaryFunctionsKeepTheirLexicalPrivateNames(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
