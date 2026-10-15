using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StaticSymbolMethodInheritanceTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["precedence-and-receiver", """
            const key = Symbol("method");
            class Base { static [key](): any { return 1; } }
            class Child extends Base {}
            class Override extends Base {
                static [key](expected?: any): any { return expected === undefined ? 3 : this === expected; }
            }
            class Leaf extends Override {}
            const base: any = Base, child: any = Child, overridden: any = Override, leaf: any = Leaf;
            const original: any = base[key], own: any = overridden[key];
            base[key] = null;
            console.log(overridden[key]());
            console.log(base[key] === null, child[key] === null, own !== original,
                overridden[key] === own, leaf[key] === own,
                overridden[key](overridden), leaf[key](leaf));
            base[key] = undefined;
            console.log(base[key] === undefined, child[key] === undefined, leaf[key](), leaf[key] === own);
            const replacement: any = function(this: any, expected: any) { return this === expected ? 4 : 9; };
            base[key] = replacement;
            console.log(base[key] === replacement, child[key] === replacement, child[key](child), overridden[key]());
            overridden[key] = function(this: any) { return this === overridden ? 6 : 7; };
            console.log(overridden[key](), leaf[key](), leaf[key] === overridden[key], child[key](child));
            """, "3\ntrue true true true true true true\ntrue true 3 true\ntrue true 4 3\n6 7 true 4\n"];

        yield return ["accessors-and-symbol-identity", """
            const first = Symbol("same"), second = Symbol("same");
            class Base {
                static [first]() { return "base-first"; }
                static [second]() { return "base-second"; }
            }
            class Middle extends Base {
                static [first]() { return "middle-first"; }
                static [second]() { return "middle-second"; }
            }
            class Leaf extends Middle {}
            class Blocker extends Middle {}
            class Deep extends Blocker {}
            const base: any = Base, middle: any = Middle, leaf: any = Leaf;
            const blocker: any = Blocker, deep: any = Deep;
            const firstMethod: any = middle[first], secondMethod: any = middle[second];
            let reads = 0;
            Object.defineProperty(base, first, { get: function() { reads++; return null; }, configurable: true });
            base[second] = undefined;
            console.log(middle[first](), leaf[first](), middle[second](), leaf[second](), reads);
            console.log(firstMethod === middle[first], firstMethod === leaf[first], firstMethod === secondMethod);
            leaf[first] = null;
            console.log(leaf[first] === null, leaf[second](), middle[first](), reads);
            Object.defineProperty(blocker, first, {
                get: function(this: any): any { reads++; return this === deep ? undefined : null; }, configurable: true
            });
            console.log(deep[first] === undefined, reads, deep[second]());
            """, "middle-first middle-first middle-second middle-second 0\ntrue true false\ntrue middle-second middle-first 0\ntrue 1 middle-second\n"];

        yield return ["generic-constructors", """
            const key = Symbol("method");
            class Base<T> { static [key](): any { return 1; } }
            class Override<T> extends Base<T> {
                static [key](expected?: any): any { return [this === expected, 3]; }
            }
            class Leaf<T> extends Override<T> {}
            const base: any = Base, overridden: any = Override, leaf: any = Leaf;
            const method: any = overridden[key];
            base[key] = null;
            const result: any = leaf[key](leaf);
            console.log(method === overridden[key], method === leaf[key], result[0], result[1], base[key] === null);
            base[key] = undefined;
            console.log(leaf[key](leaf)[0], leaf[key](leaf)[1], method === leaf[key]);
            """, "true true true 3 true\ntrue 3 true\n"];

        yield return ["expression-definitions", """
            const key = Symbol("method");
            class Base { static [key](): any { return 1; } }
            const base: any = Base;
            base[key] = null;
            function make(value: number) {
                return class Named extends Base {
                    static [key](expected?: any): any { return [this === expected, value, Named]; }
                };
            }
            const first: any = make(7), second: any = make(9);
            const firstMethod: any = first[key];
            console.log(first[key] === firstMethod, first[key] === second[key]);
            const one: any = first[key](first), two: any = second[key](second);
            console.log(one[0], one[1], one[2] === first, two[0], two[1], two[2] === second);
            base[key] = undefined;
            console.log(first[key](first)[1], second[key](second)[1], first[key] === firstMethod);
            """, "true false\ntrue 7 true true 9 true\n7 9 true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void NearestStaticSymbolMemberRetainsItsValueAndCallReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
