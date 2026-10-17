using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InheritedProxySymbolSetTests
{
    public const string IssueSource = """
        function* values() { yield 1; }
        const fn: any = values, key = Symbol("key"), events: string[] = [], target = {};
        const handler: any = {
            set(actual: any, property: any, value: any, receiver: any) {
                events.push("set");
                console.log(actual === target, property === key, value === 1, receiver === fn);
                return true;
            },
            getOwnPropertyDescriptor() { events.push("descriptor"); return undefined; },
            getPrototypeOf() { events.push("prototype"); return null; }
        };
        Object.setPrototypeOf(Object.getPrototypeOf(fn), new Proxy(target, handler));
        events.length = 0;
        fn[key] = 1;
        console.log(events.join(","), Object.hasOwn(fn, key));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["generator", IssueSource, "true true true true\nset false\n"];
        yield return ["async-generator", IssueSource.Replace("function*", "async function*"),
            "true true true true\nset false\n"];
        foreach (var declaration in new[]
        {
            "function values() {}",
            "const values: any = () => 1;",
            "function base() {} const values: any = base.bind(null);"
        })
            yield return [declaration, IssueSource.Replace("function* values() { yield 1; }", declaration)
                .Replace("Object.setPrototypeOf(Object.getPrototypeOf(fn), new Proxy(target, handler));",
                    "Object.setPrototypeOf(fn, Object.create(new Proxy(target, handler)));"),
                "true true true true\nset false\n"];
        yield return ["rejection-and-shadowing", """
            function* values() { yield 1; }
            const fn: any = values, key = Symbol("key"), events: string[] = [];
            Object.setPrototypeOf(Object.getPrototypeOf(fn), new Proxy({}, {
                set(target: any, property: any, value: any, receiver: any) {
                    events.push("set"); return false;
                }
            }));
            fn[key] = 1;
            console.log(events.join(","), Object.hasOwn(fn, key));
            function assignStrict() { "use strict"; fn[key] = 2; }
            try { assignStrict(); } catch (error) { console.log(error instanceof TypeError); }
            console.log(events.join(","), Object.hasOwn(fn, key));
            Object.defineProperty(fn, key, { value: 3, writable: true, configurable: true });
            fn[key] = 4;
            console.log(fn[key], events.join(","));
            """, "set false\ntrue\nset,set false\n4 set,set\n"];
        yield return ["abrupt-completion", """
            function* values() { yield 1; }
            const fn: any = values, key = Symbol("key"), marker = {};
            const prototype: any = Object.getPrototypeOf(fn);
            Object.setPrototypeOf(prototype, new Proxy({}, { set() { throw marker; } }));
            try { fn[key] = 1; } catch (error) { console.log(error === marker); }
            console.log(Object.hasOwn(fn, key));
            const revoked = Proxy.revocable({}, {});
            Object.setPrototypeOf(prototype, revoked.proxy);
            revoked.revoke();
            try { fn[key] = 2; } catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.hasOwn(fn, key));
            """, "true\nfalse\ntrue\nfalse\n"];
        yield return ["forwarding", """
            function* values() { yield 1; }
            const fn: any = values, key = Symbol("key"), target: any = {};
            Object.defineProperty(target, key, {
                set(this: any, value: any) { console.log(this === fn, value); }, configurable: true
            });
            Object.setPrototypeOf(Object.getPrototypeOf(fn), new Proxy(target, {
                getOwnPropertyDescriptor() { throw new Error("unexpected descriptor trap"); },
                getPrototypeOf() { throw new Error("unexpected prototype trap"); }
            }));
            fn[key] = 7;
            console.log(Object.hasOwn(fn, key));
            """, "true 7\nfalse\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InheritedProxyReceivesSymbolAssignment(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
