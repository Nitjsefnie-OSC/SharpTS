using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ErrorCallableConstructorTests
{
    [Theory, ModeData]
    public void NativeErrorsCalledThroughFunctionMethodsKeepTheirBrand(ExecutionMode mode)
    {
        const string source = """
            for (const ctor of [Error, TypeError, RangeError, ReferenceError, SyntaxError, URIError, EvalError]) {
                const alias: any = ctor;
                const called = alias.call({}, "call");
                const applied = alias.apply(null, ["apply"]);
                const reflected = Reflect.apply(alias, undefined, ["reflect"]);
                const direct = alias("direct");
                console.log(called instanceof ctor, called instanceof Error, called.message);
                console.log(applied instanceof ctor, applied.message);
                console.log(reflected instanceof ctor, reflected.message);
                console.log(direct instanceof ctor, direct.message);
                console.log(called !== applied);
            }
            """;
        if (mode == ExecutionMode.Compiled)
            Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal(string.Concat(Enumerable.Repeat(
            "true true call\ntrue apply\ntrue reflect\ntrue direct\ntrue\n", 7)), TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void DynamicErrorCallsCoerceMessageAndInstallCause(ExecutionMode mode)
    {
        const string source = """
            let coercions = 0;
            const message = { toString() { coercions++; return "message"; } };
            const error = Error.call(null, message, { cause: 42 });
            console.log(error instanceof Error, error.message, error.cause, coercions);
            const empty = TypeError.apply(null, []);
            console.log(empty instanceof TypeError, empty.message, Object.hasOwn(empty, "message"));
            console.log(Error.call(null, undefined).message);
            class Child extends Error {}
            try { (Child as any).call(null, "invalid"); }
            catch (error) { console.log(error instanceof TypeError); }
            """;
        Assert.Equal("true message 42 1\ntrue  false\n\ntrue\n", TestHarness.Run(source, mode));
    }
}
