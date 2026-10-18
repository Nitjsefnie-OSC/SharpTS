using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ProxySymbolDefinitionTests
{
    // #2291 tracks the distinct compiled Symbol defineProperty trap bypass.
    [Fact]
    public void SymbolIdentityAndThrownTrapValueArePreserved()
    {
        const string source = """
            const key: unique symbol = Symbol('probe');
            const token = {}, state = { traps: 0 };
            const proxy: any = new Proxy({}, { defineProperty(target, property, descriptor) {
                state.traps++; console.log(property === key); throw token;
            } });
            try {
                Object.defineProperty(proxy, key, {
                    value: 1, writable: true, enumerable: true, configurable: true
                });
                console.log('no throw');
            } catch (error) { console.log(error === token); }
            console.log(state.traps);
            """;
        Assert.Equal("true\ntrue\n1\n", TestHarness.RunInterpreted(source));
    }

    [Fact]
    public void TraplessNestedProxiesForwardTheOriginalSymbol()
    {
        const string source = """
            const key = Symbol('slot'), target: any = {};
            const proxy: any = new Proxy(new Proxy(target, {}), {});
            Object.defineProperty(proxy, key, {
                value: 7, writable: true, enumerable: true, configurable: true
            });
            const descriptor: any = Object.getOwnPropertyDescriptor(proxy, key);
            console.log(target[key], target['Symbol(slot)'] === undefined,
                Object.getOwnPropertySymbols(target)[0] === key);
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            """;
        Assert.Equal("7 true true\n7 true true true\n", TestHarness.RunInterpreted(source));
    }

    [Fact]
    public void BooleanTrapRejectionThrowsGuestTypeError()
    {
        const string source = """
            const key = Symbol('slot'), target: any = {};
            let calls = 0;
            const proxy: any = new Proxy(target, {
                defineProperty(inner, property, descriptor) {
                    calls++; console.log(property === key); return false;
                }
            });
            try { Object.defineProperty(proxy, key, { value: 1 }); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(calls, Object.hasOwn(target, key));
            """;
        Assert.Equal("true\ntrue\n1 false\n", TestHarness.RunInterpreted(source));
    }
}
