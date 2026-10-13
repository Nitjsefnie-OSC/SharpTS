using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrivateNameShadowingTests
{
    public const string IssueSource = """
        class C {
            #m() { return 'outer'; }
            B = class {
                #m = 'inner';
                read(object: any) { return object.#m; }
            };
        }
        const outer = new C();
        const Inner = outer.B;
        const inner = new Inner();
        console.log(inner.read(inner));
        """;

    public const string FieldSource = """
        class Outer {
            #m() { return "outer"; }
            Inner = class {
                #m = "inner";
                read(value: any) { return value.#m; }
                write(value: any, next: string) { value.#m = next; }
                has(value: any) { return #m in value; }
            };
        }
        const outer = new Outer(), Inner = outer.Inner, inner = new Inner();
        console.log(inner.read(inner), inner.has(inner), inner.has(outer));
        inner.write(inner, "changed");
        console.log(inner.read(inner));
        try { inner.read(outer); } catch (error) { console.log(error instanceof TypeError); }
        try { inner.write(outer, "bad"); } catch (error) { console.log(error instanceof TypeError); }
        console.log(inner.read(inner));
        """;

    public const string MethodSource = """
        class Outer {
            #m = "outer";
            Inner = class {
                #m() { return "inner"; }
                extract(value: any) { return value.#m; }
                has(value: any) { return #m in value; }
            };
        }
        const outer = new Outer(), Inner = outer.Inner, inner = new Inner();
        const method = inner.extract(inner);
        console.log(typeof method, method === inner.extract(inner), method.call(inner),
            inner.has(inner), inner.has(outer));
        try { inner.extract(outer); } catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string FacetSource = """
        class StaticOuter {
            static #m() { return "outer"; }
            make() { return class Inner {
                #m = "instance";
                read(value: any) { return value.#m; }
            }; }
        }
        class InstanceOuter {
            #m() { return "outer"; }
            Inner = class Inner {
                static #m = "static";
                static read() { return this.#m; }
                static has(value: any) { return #m in value; }
            };
        }
        const First = new StaticOuter().make(), first = new First();
        const outer = new InstanceOuter(), Second = outer.Inner;
        console.log(first.read(first));
        console.log(Second.read(), Second.has(Second), Second.has(outer), Second.has(new Second()));
        try { Second.read.call(InstanceOuter); } catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string NearestSource = """
        class Outer {
            #m() { return "outer-method"; }
            #other() { return "outer-control"; }
            make() {
                class Middle {
                    #m = "middle";
                    make() {
                        class Inner {
                            read(value: any) { return value.#m; }
                            has(value: any) { return #m in value; }
                            callOuter(value: Outer) { return value.#other(); }
                        }
                        return new Inner();
                    }
                }
                const middle = new Middle();
                return [middle, middle.make()];
            }
        }
        const outer = new Outer(), values: any = outer.make();
        const middle = values[0], inner = values[1];
        console.log(inner.read(middle), inner.has(middle), inner.has(outer), inner.callOuter(outer));
        try { inner.read(outer); } catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string SuspensionSource = """
        class Outer {
            #m() { return "outer"; }
            make() { return class Inner {
                #m = "inner";
                arrow(value: any) { const read = () => value.#m; return read(); }
                async read(value: any) { await Promise.resolve(0); return value.#m; }
                *values(value: any) { yield value.#m; }
                async *later(value: any) { await Promise.resolve(0); yield value.#m; }
            }; }
        }
        async function main() {
            const Inner = new Outer().make(), inner = new Inner();
            console.log(inner.arrow(inner));
            console.log(await inner.read(inner));
            console.log(inner.values(inner).next().value);
            console.log((await inner.later(inner).next()).value);
        }
        main().catch(error => console.log(error));
        """;

    public const string GenericSource = """
        class Outer<T> {
            #m() { return "outer"; }
            make(value: T) { return class Inner {
                #m = value;
                read(object: any) { return object.#m; }
                has(object: any) { return #m in object; }
            }; }
        }
        const outer = new Outer<number>();
        const First = outer.make(3), Second = outer.make(4);
        const first = new First(), second = new Second();
        console.log(first.read(first), second.read(second), first.has(first), first.has(second));
        try { first.read(second); } catch (error) { console.log(error instanceof TypeError); }
        const Third = new Outer<string>().make("five");
        const third = new Third();
        console.log(third.read(third));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "inner\n"];
        yield return ["field", FieldSource, "inner true false\nchanged\ntrue\ntrue\nchanged\n"];
        yield return ["method", MethodSource, "function true inner true false\ntrue\n"];
        yield return ["facets", FacetSource, "instance\nstatic true false false\ntrue\n"];
        yield return ["nearest", NearestSource, "middle true false outer-control\ntrue\n"];
        yield return ["suspension", SuspensionSource, "inner\ninner\ninner\ninner\n"];
        yield return ["generic", GenericSource, "3 4 true false\ntrue\nfive\n"];
    }

    public static IEnumerable<object[]> ParityCases()
    {
        foreach (var row in Cases())
        {
            yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory]
    [MemberData(nameof(ParityCases))]
    public void NearestPrivateNameDeterminesMemberKindAndBrand(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void SameLeafOwnersInDifferentModulesKeepTheirNearestNames(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["left.ts"] = """
                export class Outer {
                    #m() { return "left-outer"; }
                    make() { return class Inner {
                        #m = "left";
                        read(value: any) { return value.#m; }
                        has(value: any) { return #m in value; }
                    }; }
                }
                """,
            ["right.ts"] = """
                export class Outer {
                    #m() { return "right-outer"; }
                    make() { return class Inner {
                        #m = "right";
                        read(value: any) { return value.#m; }
                        has(value: any) { return #m in value; }
                    }; }
                }
                """,
            ["main.ts"] = """
                import { Outer as Left } from './left';
                import { Outer as Right } from './right';
                const First = new Left().make(), Second = new Right().make();
                const first = new First(), second = new Second();
                console.log(first.read(first), second.read(second));
                console.log(first.has(first), first.has(second));
                try { first.read(second); } catch (error) { console.log(error instanceof TypeError); }
                """
        };
        Assert.Equal("left right\ntrue false\ntrue\n", TestHarness.RunModules(files, "main.ts", mode));
    }
}
