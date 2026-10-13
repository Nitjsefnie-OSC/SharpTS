using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NativeConstructorValueTests
{
    internal const string ReadSource = """
        const N: any = Number, S: any = String, B: any = Boolean, O: any = Object, I: any = BigInt;
        console.log(typeof N.Parse, typeof S.Concat, typeof B.Parse, typeof O.ReferenceEquals, typeof I.Parse);
        console.log(typeof N.MaxValue, typeof S.Empty, typeof B.TrueString, typeof I.Zero);
        console.log(N.isFinite(4), N.isNaN(N.NaN), S.fromCharCode(65), O.keys({ x: 1 }).join(","), I.asIntN(8, 257n).toString());
        console.log(N.MAX_VALUE === Number.MAX_VALUE, N.MIN_VALUE === Number.MIN_VALUE,
            N.POSITIVE_INFINITY === Infinity, N.NEGATIVE_INFINITY === -Infinity,
            N.MAX_SAFE_INTEGER === 9007199254740991, N.MIN_SAFE_INTEGER === -9007199254740991,
            N.EPSILON === Number.EPSILON, N.isNaN(N.NaN));
        const symbols: any = Symbol;
        console.log(symbols.iterator === Symbol.iterator);
        class User { static Parse() { return 42; } static MaxValue = 43; }
        const U: any = User;
        console.log(U.Parse(), U.MaxValue);
        """;

    internal const string ReadExpected = "undefined undefined undefined undefined undefined\n"
        + "undefined undefined undefined undefined\ntrue true A x 1\n"
        + "true true true true true true true true\ntrue\n42 43\n";

    [Theory, ModeData]
    public void NativeConstructorReadsUseJavaScriptStaticsAndKeepUserClassMembers(ExecutionMode mode)
    {
        Assert.Equal(ReadExpected, TestHarness.Run(ReadSource, mode));
    }

    [Fact]
    public void CompiledNativeConstructorFunctionPrototypeRemainsAvailable()
    {
        // The interpreter's Number namespace does not yet expose these inherited members.
        const string source = """
            const N: any = Number;
            console.log(N.constructor === Function, typeof N.call, typeof N.apply, typeof N.bind);
            """;
        Assert.Equal("true function function function\n", TestHarness.Run(source, ExecutionMode.Compiled));
    }

    [Fact]
    public void CompiledStoredNativeDescriptorsExpandosAndDeletionsWinOverStaticInventory()
    {
        // The interpreter's Object namespace does not yet read a redefined keys accessor.
        const string source = """
            const O: any = Object;
            O.ReferenceEquals = () => 17;
            console.log(O.ReferenceEquals(), O.hasOwn(O, "ReferenceEquals"));
            delete O.ReferenceEquals;
            console.log(typeof O.ReferenceEquals);
            const original = O.keys;
            Object.defineProperty(O, "keys", { get: () => () => ["changed"], configurable: true });
            console.log(O.keys({ x: 1 }).join(","));
            delete O.keys;
            console.log(typeof O.keys);
            O.keys = original;
            console.log(O.keys({ x: 1 }).join(","));
            """;
        Assert.Equal("17 true\nundefined\nchanged\nundefined\nx\n", TestHarness.Run(source, ExecutionMode.Compiled));
    }

    [Theory, ModeData]
    public void NativeErrorDescriptorChangesAndDeletionRemainObservable(ExecutionMode mode)
    {
        const string source = """
            const E: any = Error;
            Object.defineProperty(E, "custom", { value: 17, configurable: true });
            console.log(E.custom);
            delete E.custom;
            console.log(typeof E.custom);
            const original = E.isError;
            E.isError = () => false;
            console.log(E.isError(new Error()));
            delete E.isError;
            console.log(typeof E.isError);
            E.isError = original;
            console.log(E.isError(new Error()));
            """;
        Assert.Equal("17\nundefined\nfalse\nundefined\ntrue\n", TestHarness.Run(source, mode));
    }
}
