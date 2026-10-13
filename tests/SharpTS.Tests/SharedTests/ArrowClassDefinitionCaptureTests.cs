using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArrowClassDefinitionCaptureTests
{
    public const string IssueSource = """
        const C = class {
            #m = 42;
            arrow(value: any) { return (() => value.#m)(); }
        };
        const c = new C();
        console.log(c.arrow(c));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["captured-parameter", IssueSource, "42\n"];
        yield return ["escaping-private-operations", """
            class Base { label() { return "base"; } }
            function make(seed: any) {
                return class extends Base {
                    #value = seed;
                    #read() { return this.#value; }
                    read(value: any) { return () => value.#value; }
                    write(value: any) { return (next: any) => value.#value = next; }
                    method(value: any) { return () => value.#read; }
                    call(value: any) { return () => value.#read(); }
                    has(value: any) { return () => #value in value; }
                };
            }
            const First = make(1), Second = make(2);
            const first: any = new First(), second: any = new Second();
            const left = first.read(first), right = second.read(second);
            const raw = first.method(first)();
            console.log(left(), right(), first.label(), first.read.call({}, first)());
            console.log(raw.call(first), first.call(first)(), first.has(first)(), first.has(second)());
            try { first.read(second)(); } catch (error) { console.log(error instanceof TypeError); }
            first.write(first)(9);
            console.log(left(), right(), raw.call(first));
            """, "1 2 base 1\n1 1 true false\ntrue\n9 2 9\n"];
        yield return ["nested-and-default-arrows", """
            const C = class {
                #value = 11;
                nested(value: any) { return () => () => value.#value; }
                defaults(value: any) { return (get: any = () => value.#value) => get(); }
            };
            const instance: any = new C();
            console.log(instance.nested(instance)()(), instance.defaults(instance)());
            """, "11 11\n"];
        yield return ["async-private-operations", """
            function make(seed: any) {
                return class {
                    #value = seed;
                    #read() { return this.#value; }
                    async read(value: any) {
                        await Promise.resolve(0);
                        return await (async () => { await Promise.resolve(0); return value.#value; })();
                    }
                    async syncRead(value: any) {
                        await Promise.resolve(0);
                        return (() => value.#value)();
                    }
                    async method(value: any) {
                        return await (async () => { await Promise.resolve(0); return value.#read; })();
                    }
                    async has(value: any) {
                        return await (async () => { await Promise.resolve(0); return #value in value; })();
                    }
                    async nested(value: any) {
                        return await (async () => {
                            await Promise.resolve(0);
                            return await (async () => value.#value)();
                        })();
                    }
                };
            }
            async function run() {
                try {
                const First = make(3), Second = make(4);
                const first: any = new First(), second: any = new Second();
                const left = first.read(first), right = second.read(second);
                console.log(await left, await right);
                const raw = await first.method(first);
                console.log(raw.call(first), await first.has(first), await first.has(second), await first.nested(first));
                try { await first.read(second); } catch (error) { console.log(error instanceof TypeError); }
                console.log(await second.read(second), await first.read(first), await first.syncRead(first));
                } catch (error) { console.log("unexpected failure", error); }
            }
            run();
            """, "3 4\n3 true false 3\ntrue\n4 3 3\n"];
        yield return ["class-definition-boundaries", """
            const Base: any = class { label() { return "base"; } };
            const Outer = class {
                #parent = Base;
                create(value: any) {
                    return () => class extends value.#parent {
                        #value = 7;
                        read(receiver: any) { return () => receiver.#value; }
                    };
                }
            };
            const outer = new Outer(), Inner = outer.create(outer)();
            console.log(typeof Inner);
            try { outer.create({})(); } catch (error) { console.log(error instanceof TypeError); }
            """, "function\ntrue\n"];
        yield return ["nested-class-creation", """
            function make(seed: any) {
                return class {
                    #value = seed;
                    create() {
                        return () => class {
                            #inner = 11;
                            read(value: any) { return () => value.#inner; }
                        };
                    }
                };
            }
            const First = make(1), Second = make(2);
            const Left = new First().create()(), Right = new Second().create()();
            const left: any = new Left(), right: any = new Right();
            console.log(left.read(left)(), right.read(right)(), Left === Right);
            try { left.read(right)(); } catch (error) { console.log(error instanceof TypeError); }
            """, "11 11 false\ntrue\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void PrivateArrowsCaptureTheirLexicalClassDefinition(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
