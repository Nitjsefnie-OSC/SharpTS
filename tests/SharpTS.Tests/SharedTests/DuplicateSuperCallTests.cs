using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DuplicateSuperCallTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            const state = { trace: "" };
            class Base {
                value = (state.trace += "f", 1);
                constructor() { state.trace += "b"; }
            }
            class Derived extends Base {
                child = (state.trace += "d", 1);
                constructor() {
                    super();
                    try { super(); console.log(false, state.trace); }
                    catch (error) { console.log(error instanceof ReferenceError, state.trace); }
                }
            }
            new Derived();
            """, "true fbdfb\n"];
        yield return ["parent-throw-precedes-binding-error", """
            const token = {}, seen: any[] = [];
            class Base {
                value = seen.length + 1;
                constructor(fail: boolean) { seen.push(this); if (fail) throw token; }
            }
            class Derived extends Base {
                child = 7;
                constructor() {
                    super(false);
                    try { super(true); } catch (error) { console.log(error === token); }
                    console.log(this === seen[0], seen[0] !== seen[1], this.value, seen[1].value,
                        this.child, Object.hasOwn(seen[1], 'child'));
                }
            }
            new Derived();
            """, "true\ntrue true 1 2 7 false\n"];
        yield return ["failed-parent-retry-retains-escaped-brands", """
            const seen: any[] = [], token = {};
            let sequence = 0;
            class Base {
                #id = ++sequence;
                accessor value: number = sequence;
                constructor(fail: boolean) { seen.push(this); if (fail) throw token; }
                read() { return this.#id; }
            }
            class Derived extends Base {
                constructor() {
                    try { super(true); } catch (error) { console.log(error === token); }
                    super(false);
                }
            }
            const result: any = new Derived();
            console.log(seen.length, seen[0] !== seen[1], seen[0].read(), seen[1].read(),
                seen[0].value, seen[1].value, result === seen[1]);
            """, "true\n2 true 1 2 1 2 true\n"];
        yield return ["failed-derived-initializer-does-not-retry-elements", """
            const token = {};
            let parents = 0, children = 0;
            function fail(): any { children++; throw token; }
            class Base { constructor() { parents++; } }
            class Derived extends Base {
                value = fail();
                constructor() {
                    try { super(); } catch (error) { console.log(error === token); }
                    try { super(); } catch (error) { console.log(error instanceof ReferenceError); }
                    console.log(parents, children, Object.hasOwn(this, 'value'));
                }
            }
            new Derived();
            """, "true\ntrue\n2 1 false\n"];
        yield return ["native-array-and-error-allocations", """
            class Values extends Array<number> {
                constructor() {
                    super(1, 2);
                    const first = this;
                    try { super(3, 4); } catch (error) { console.log(error instanceof ReferenceError); }
                    console.log(this === first, this.join(','));
                }
            }
            class Failure extends Error {
                constructor() {
                    super('first');
                    try { super('second'); } catch (error) { console.log(error instanceof ReferenceError); }
                    console.log(this.message);
                }
            }
            new Values(); new Failure();
            """, "true\ntrue 1,2\ntrue\nfirst\n"];
        yield return ["native-promise-capabilities", """
            let calls = 0;
            class Result extends Promise<any> {
                constructor() {
                    super((resolve: any) => { calls++; resolve('first'); });
                    try { super((resolve: any) => { calls++; resolve('second'); }); }
                    catch (error) { console.log(error instanceof ReferenceError); }
                }
            }
            Object.defineProperty(Result, Symbol.species, { value: Promise });
            const result: any = new Result();
            console.log(calls);
            result.then((value: any) => console.log(value));
            """, "true\n2\nfirst\n"];
    }

    [Theory, MemberData(nameof(Cases))]
    public void ParentConstructionCompletesBeforeDuplicateBindingIsRejected(string name, string source, string expected)
    {
        // #2568 tracks compiled duplicate-super rejection separately.
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunInterpreted(source));
    }
}
