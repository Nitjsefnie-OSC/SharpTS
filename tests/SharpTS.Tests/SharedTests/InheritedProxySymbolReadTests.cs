using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InheritedProxySymbolReadTests
{
    public const string IssueSource = """
        const prototype: any = new Proxy({}, {
            get(target: any, key: any, receiver: any) {
                console.log(key === Symbol.hasInstance, receiver === matcher);
                return function() { return 42; };
            }
        });
        const matcher: any = Object.create(prototype);
        console.log(typeof matcher[Symbol.hasInstance]);
        """;

    public const string ReceiverSource = """
        const key = Symbol("ordinary"), target = {}, state: any = { calls: 0, receiver: null };
        function result() { return 42; }
        const proxy: any = new Proxy(target, {
            get(actual: any, property: any, receiver: any) {
                state.calls++; state.receiver = receiver;
                console.log(actual === target, property === key);
                return result;
            }
        });
        const child: any = Object.create(proxy), grandchild: any = Object.create(child);
        console.log(proxy[key] === result, state.receiver === proxy, state.calls);
        console.log(grandchild[key] === result, state.receiver === grandchild, state.calls);
        Object.defineProperty(grandchild, key, { value: undefined, configurable: true });
        console.log(grandchild[key] === undefined, state.calls);
        const own = () => 99;
        Object.defineProperty(grandchild, key, { value: own });
        console.log(grandchild[key] === own, state.calls);
        console.log(child[key] === result, state.receiver === child, state.calls);
        """;

    public const string ErrorsSource = """
        const key = Symbol("errors"), sentinel = {};
        const throwing: any = Object.create(new Proxy({}, { get() { throw sentinel; } }));
        try { const ignored = throwing[key]; } catch (error) { console.log(error === sentinel); }
        const revoked = Proxy.revocable({}, {}), revokedChild: any = Object.create(revoked.proxy);
        revoked.revoke();
        try { const ignored = revokedChild[key]; } catch (error) { console.log(error instanceof TypeError); }
        const fixedTarget: any = {};
        Object.defineProperty(fixedTarget, key, { value: sentinel });
        const fixedChild: any = Object.create(new Proxy(fixedTarget, { get() { return {}; } }));
        try { const ignored = fixedChild[key]; } catch (error) { console.log(error instanceof TypeError); }
        const getterlessTarget: any = {};
        Object.defineProperty(getterlessTarget, key, { get: undefined, configurable: false });
        const getterlessChild: any = Object.create(new Proxy(getterlessTarget, { get() { return 1; } }));
        try { const ignored = getterlessChild[key]; } catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string ForwardingSource = """
        const key = Symbol("forward"), state: any = { reads: 0, receiver: null }, prototype: any = {};
        Object.defineProperty(prototype, key, {
            get: function(this: any) { state.reads++; state.receiver = this; return this.slot; },
            configurable: true
        });
        const target: any = Object.create(prototype), proxy: any = new Proxy(new Proxy(target, {}), {});
        const receiver: any = Object.create(proxy);
        Object.defineProperty(receiver, "slot", { value: 7 });
        console.log(receiver[key], state.reads, state.receiver === receiver);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "true true\nfunction\n"];
        yield return ["receivers-and-shadows", ReceiverSource,
            "true true\ntrue true 1\ntrue true\ntrue true 2\ntrue 2\ntrue 2\ntrue true\ntrue true 3\n"];
        yield return ["errors-and-invariants", ErrorsSource, "true\ntrue\ntrue\ntrue\n"];
        yield return ["forwarding", ForwardingSource, "7 1 true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void InheritedSymbolReadsPreserveProxyGetSemantics(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
