using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.RuntimeTests;

public sealed class FunctionIntrinsicRealmIsolationTests
{
    [Theory]
    [InlineData("bind", 1)]
    [InlineData("call", 1)]
    [InlineData("apply", 2)]
    public void FreshInterpretersOwnTheirMutableFunctionIntrinsics(string member, int arity)
    {
        string mutateFirstRealm = $$"""
            function F() {}
            const method: any = Function.prototype.{{member}};
            console.log(delete method.name, delete method.length);
            console.log(F.{{member}}.name, F.{{member}}.length);
            Object.defineProperty(method, "apply", { value: 99, configurable: true });
            console.log(method.apply);
            Object.preventExtensions(method);
            console.log(Object.isExtensible(method));
            """;
        Assert.Equal("true true\n 0\n99\nfalse\n", TestHarness.RunInterpreted(mutateFirstRealm));

        string invocation = member switch
        {
            "bind" => "F.bind(null)()",
            "call" => "F.call(null)",
            _ => "F.apply(null, [])"
        };
        string inspectFreshRealm = $$"""
            function F() { return 42; }
            const method: any = Function.prototype.{{member}};
            console.log(method.name, method.length, Object.hasOwn(method, "name"),
                Object.hasOwn(method, "length"), Object.isExtensible(method));
            console.log(F.{{member}} === method, F.{{member}} === F.{{member}});
            console.log(method.bind === Function.prototype.bind, method.call === Function.prototype.call,
                method.apply === Function.prototype.apply);
            console.log({{invocation}});
            """;
        Assert.Equal($"{member} {arity} true true true\ntrue true\ntrue true true\n42\n",
            TestHarness.RunInterpreted(inspectFreshRealm));
    }
}
