using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArrayConstructorFunctionHelperTests
{
    public const string CanonicalSource = """
        const A: any = Array, prototype: any = Function.prototype;
        console.log(typeof A.call, typeof A.apply, typeof A.bind);
        console.log(A.call === prototype.call, A.apply === prototype.apply, A.bind === prototype.bind);
        console.log(A.call === A["call"], A.apply === Reflect.get(A, "apply"), A.bind === A.bind);
        console.log(Object.hasOwn(A, "call"), Object.hasOwn(A, "apply"), Object.hasOwn(A, "bind"));
        const isArray = A.isArray;
        console.log(isArray === A.isArray, isArray([]), isArray({}));
        """;

    public const string InvocationSource = """
        const A: any = Array, receiver: any = { marker: 7 };
        console.log(A.call(receiver, 1, 2).join(","), A.apply(receiver, [3, 4]).join(","),
            A.bind(receiver, 5)(6).join(","));
        const once = A.bind(receiver, "a"), twice = once.bind({ marker: 8 }, "b");
        console.log(twice("c").join(","), receiver.marker, Object.keys(receiver).join(","));
        const call = A.call;
        try { call({}, 1, 2); }
        catch (error) { console.log(error instanceof TypeError); }
        console.log(call.call(A, receiver, "x", "y").join(","));
        """;

    public const string ArgumentsSource = """
        const A: any = Array;
        const empty = A.call(null), sparse = A.apply(null, [3]);
        console.log(empty.length, sparse.length, Object.hasOwn(sparse, "0"), Object.hasOwn(sparse, "2"));
        const missing = A.bind(null, undefined)(), nil = A.call(null, null);
        console.log(missing.length, Object.hasOwn(missing, "0"), missing[0] === undefined,
            nil.length, nil[0] === null);
        const item = { tag: 1 }, first = A.apply(null, [item]), second = A.bind(null, item)();
        console.log(first[0] === item, second[0] === item, first !== second);
        try { A.call(null, -1); }
        catch (error) { console.log(error instanceof RangeError); }
        try { A.apply(null, [1.5]); }
        catch (error) { console.log(error instanceof RangeError); }
        """;

    public const string PrototypeDescriptorSource = """
        const A: any = Array, prototype: any = Function.prototype;
        const original: any = Object.getOwnPropertyDescriptor(prototype, "call"), helper = prototype.call;
        const state = { reads: 0, receivers: 0 }, sentinel = {};
        Object.defineProperty(prototype, "call", { value: 13, writable: true, configurable: true });
        const data = A.call === 13 && A["call"] === 13 && Reflect.get(A, "call") === 13;
        Object.defineProperty(prototype, "call", {
            get: function(this: any) { state.reads++; if (this === A) state.receivers++; return 17; },
            configurable: true
        });
        const accessor = A.call === 17 && A["call"] === 17 && Reflect.get(A, "call") === 17;
        Object.defineProperty(prototype, "call", { get() { throw sentinel; }, configurable: true });
        let exactThrow = false;
        try { const ignored = A.call; } catch (error) { exactThrow = error === sentinel; }
        const removed = delete prototype.call;
        const absent = A.call === undefined && A["call"] === undefined && Reflect.get(A, "call") === undefined;
        Object.defineProperty(prototype, "call", original);
        console.log(data, accessor, state.reads, state.receivers, exactThrow, removed, absent, A.call === helper);
        """;

    public const string OwnDescriptorSource = """
        const A: any = Array, helper = A.bind, state = { reads: 0, receivers: 0, writes: 0 };
        Object.defineProperty(A, "bind", { value: null, writable: true, configurable: true });
        const nil = A.bind === null && A["bind"] === null && Reflect.get(A, "bind") === null;
        Object.defineProperty(A, "bind", { value: undefined });
        const missing = A.bind === undefined && A["bind"] === undefined && Reflect.get(A, "bind") === undefined;
        Object.defineProperty(A, "bind", { set(value: any) { state.writes++; }, configurable: true });
        const getterless = A.bind === undefined && A["bind"] === undefined && Reflect.get(A, "bind") === undefined;
        Object.defineProperty(A, "bind", {
            get: function(this: any) { state.reads++; if (this === A) state.receivers++; return helper; },
            configurable: true
        });
        const accessor = A.bind === helper && A["bind"] === helper && Reflect.get(A, "bind") === helper;
        const removed = delete A.bind;
        console.log(nil, missing, getterless, accessor, state.reads, state.receivers, state.writes,
            removed, !Object.hasOwn(A, "bind"));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["canonical", CanonicalSource,
            "function function function\ntrue true true\ntrue true true\nfalse false false\ntrue true false\n"];
        yield return ["invocation", InvocationSource, "1,2 3,4 5,6\na,b,c 7 marker\ntrue\nx,y\n"];
        yield return ["arguments", ArgumentsSource, "0 3 false false\n1 true true 1 true\ntrue true true\ntrue\ntrue\n"];
    }

    public static IEnumerable<object[]> DescriptorCases()
    {
        yield return ["prototype", PrototypeDescriptorSource, "true true 3 3 true true true true\n"];
        yield return ["own", OwnDescriptorSource, "true true true true 3 3 0 true true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().Concat(DescriptorCases()).SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ArrayAliasesReadLiveFunctionHelpersAndPreserveArrayCalls(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedArrayMetadataAndDeletedHelperPreserveNativeIdentity()
    {
        // Compiled alias metadata and inherited helper deletion are tracked by #2315 and #2316.
        const string observations = """

            console.log(A.length === 1, A.prototype === Array.prototype, A.bind === helper);
            """;
        Assert.Equal("true true true true 3 3 0 true true\ntrue true true\n",
            TestHarness.RunInterpreted(OwnDescriptorSource + "\n" + observations));
    }
}
