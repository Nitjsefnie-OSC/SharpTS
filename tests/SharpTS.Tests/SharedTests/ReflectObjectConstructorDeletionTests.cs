using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ReflectObjectConstructorDeletionTests
{
    public const string IssueSource = """
        const O: any = Object;
        console.log(Reflect.deleteProperty(O, "keys"), typeof O.keys);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true undefined\n"];
        yield return ["cached-builtin", """
            const O: any = Object, original = O.keys;
            console.log(typeof original, Reflect.deleteProperty(O, "keys"), typeof O.keys);
            console.log(Reflect.deleteProperty(O, "keys"), typeof O.keys);
            O.keys = original;
            console.log(O.keys === original, O.keys({ a: 1 }).join(","));
            console.log(Reflect.deleteProperty(O, "keys"), typeof O.keys);
            Object.defineProperty(O, "keys", { value: original, configurable: true, writable: true });
            console.log(O.keys === original, O.keys({ b: 2 }).join(","));
            """, "function true undefined\ntrue undefined\ntrue a\ntrue undefined\ntrue b\n"];
        yield return ["descriptors", """
            const O: any = Object;
            O.temporary = 7;
            console.log(Reflect.deleteProperty(O, "temporary"), O.temporary === undefined,
                Reflect.deleteProperty(O, "temporary"), Reflect.deleteProperty(O, "missing"));
            let getterCalls = 0;
            Object.defineProperty(O, "accessor", {
                get: () => { getterCalls++; return 5; }, configurable: true
            });
            console.log(Reflect.deleteProperty(O, "accessor"), typeof O.accessor, getterCalls);
            Object.defineProperty(O, "locked", { value: 9, configurable: false });
            console.log(Reflect.deleteProperty(O, "locked"), O.locked);
            const prototype = O.prototype;
            console.log(Reflect.deleteProperty(O, "prototype"), O.prototype === prototype);
            """, "true true true true\ntrue undefined 0\nfalse 9\nfalse true\n"];
    }

    [Fact]
    public void InterpretedObjectConstructorDeletionNormalizesPropertyKeys()
    {
        // Compiled Symbol deletion is covered separately by #2160.
        const string source = """
            {
            const O: any = Object;
            O[""] = 1;
            O["null"] = 2;
            O["undefined"] = 3;
            O["true"] = 4;
            O["false"] = 5;
            console.log(Reflect.deleteProperty(O, null as any), O["null"] === undefined, O[""]);
            console.log(Reflect.deleteProperty(O, undefined as any), O["undefined"] === undefined);
            console.log(Reflect.deleteProperty(O, true as any), O["true"] === undefined);
            console.log(Reflect.deleteProperty(O, false as any), O["false"] === undefined);
            }
            {
            const O: any = Object;
            const state = { calls: 0, fallbackCalls: 0 };
            const key: any = {
                toString() { state.calls++; return "temporary"; },
                valueOf() { state.fallbackCalls++; throw "unexpected valueOf"; }
            };
            O.temporary = 7;
            console.log(Reflect.deleteProperty(O, key), O.temporary === undefined,
                state.calls, state.fallbackCalls);
            }
            {
            const O: any = Object;
            const state = { gets: 0, calls: 0, hints: "", receivers: true };
            const key: any = {};
            Object.defineProperty(key, Symbol.toPrimitive, {
                get() {
                    state.gets++;
                    state.receivers = state.receivers && this === key;
                    return function(this: any, hint: string) {
                        state.calls++;
                        state.hints += hint;
                        state.receivers = state.receivers && this === key;
                        return "temporary";
                    };
                }
            });
            O.temporary = 7;
            console.log(Reflect.deleteProperty(O, key), O.temporary === undefined,
                state.gets, state.calls, state.hints, state.receivers);
            const original = {};
            const broken: any = {
                [Symbol.toPrimitive](hint: string) {
                    state.calls++;
                    state.hints += "," + hint;
                    throw original;
                }
            };
            O.retained = 9;
            try { Reflect.deleteProperty(O, broken); console.log("returned"); }
            catch (error) { console.log(error === original, O.retained,
                state.calls, state.hints); }
            }
            {
            const O: any = Object, symbol = Symbol("collision");
            O["Symbol(collision)"] = 7;
            console.log(Reflect.deleteProperty(O, symbol), O["Symbol(collision)"]);
            const state = { calls: 0, hint: "" };
            const key: any = {
                [Symbol.toPrimitive](hint: string) {
                    state.calls++;
                    state.hint = hint;
                    return symbol;
                }
            };
            console.log(Reflect.deleteProperty(O, key), O["Symbol(collision)"],
                state.calls, state.hint);
            }
            """;
        Assert.Equal("true true 1\ntrue true\ntrue true\ntrue true\n"
            + "true true 1 0\ntrue true 1 1 string true\ntrue 9 2 string,string\n"
            + "true 7\ntrue 7 1 string\n", TestHarness.RunInterpreted(source));
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ObjectConstructorDeletionMutatesPropertiesAndHonorsDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedObjectConstructorDeletionIsConfinedToItsRealm()
    {
        Assert.Equal("true undefined\n", TestHarness.RunInterpreted(IssueSource));
        Assert.Equal("function\n", TestHarness.RunInterpreted("console.log(typeof Object.keys);"));
    }
}
