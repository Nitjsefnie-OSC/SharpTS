using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassPrototypeDescriptorTests
{
    internal const string IssueSource = """
        const original = Error.prototype;
        console.log(Object.getOwnPropertyDescriptor(Error, "prototype")?.writable);
        (Error as any).prototype = {};
        console.log(Error.prototype === original);
        """;

    [Theory, ModeData]
    public void ErrorPrototypeAssignmentPreservesItsIntrinsicValue(ExecutionMode mode)
    {
        Assert.Equal("false\ntrue\n", TestHarness.Run(IssueSource, mode));
    }

    [Theory, ModeData]
    public void NativeAndUserClassPrototypesRejectAssignmentAndDeletion(ExecutionMode mode)
    {
        const string source = """
            class Ordinary {}
            class Derived extends Ordinary {}
            class Generic<T> {}
            class ErrorChild extends Error {}
            function make() { return class {}; }
            const first = make(), second = make();
            const constructors: any[] = [Error, TypeError, RangeError, ReferenceError,
                SyntaxError, URIError, EvalError, AggregateError,
                Ordinary, Derived, Generic, ErrorChild, first, second];
            for (const owner of constructors) {
                const original = owner.prototype;
                const descriptor: any = Object.getOwnPropertyDescriptor(owner, "prototype");
                console.log(descriptor.value === original, descriptor.writable,
                    descriptor.enumerable, descriptor.configurable);
                owner.prototype = {};
                owner["prototype"] = {};
                console.log(owner.prototype === original,
                    Object.getOwnPropertyDescriptor(owner, "prototype").value === original,
                    Object.hasOwn(owner, "prototype"));
                const receiver: any = {};
                console.log(Reflect.set(owner, "prototype", {}),
                    Reflect.set(owner, "prototype", original),
                    Reflect.set(owner, "prototype", {}, receiver),
                    Object.hasOwn(receiver, "prototype"));
                console.log(delete owner.prototype, Reflect.deleteProperty(owner, "prototype"),
                    owner.prototype === original);
            }
            console.log(first.prototype !== second.prototype,
                Derived.prototype !== Ordinary.prototype);
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat(
            "true false false false\ntrue true true\nfalse false false false\nfalse false true\n", 14)) +
            "true true\n", TestHarness.Run(source, mode));
    }

    internal const string DefinitionSource = """
        class Ordinary {}
        class Generic<T> {}
        function make() { return class {}; }
        const first = make(), second = make();
        const constructors: any[] = [Error, TypeError, Ordinary, Generic, first, second];
        for (const owner of constructors) {
            const original = owner.prototype;
            Object.defineProperty(owner, "prototype", {});
            Object.defineProperty(owner, "prototype", { value: original });
            Object.defineProperty(owner, "prototype", { writable: false });
            Object.defineProperty(owner, "prototype", { enumerable: false, configurable: false });
            console.log(owner.prototype === original,
                Reflect.defineProperty(owner, "prototype", {}),
                Reflect.defineProperty(owner, "prototype", { value: original }),
                Reflect.defineProperty(owner, "prototype", {
                    writable: false, enumerable: false, configurable: false }));
            for (const descriptor of [{ value: {} }, { value: undefined }, { value: null },
                { writable: true }, { enumerable: true }, { configurable: true },
                { get: undefined }, { set: undefined }]) {
                try { Object.defineProperty(owner, "prototype", descriptor); }
                catch (error) { console.log(error instanceof TypeError); }
                console.log(Reflect.defineProperty(owner, "prototype", descriptor),
                    owner.prototype === original);
            }
            const descriptor: any = Object.getOwnPropertyDescriptor(owner, "prototype");
            console.log(descriptor.value === original, descriptor.writable,
                descriptor.enumerable, descriptor.configurable);
        }
        """;

    internal static string DefinitionExpected => string.Concat(Enumerable.Repeat(
        "true true true true\n" + string.Concat(Enumerable.Repeat("true\nfalse true\n", 8)) +
        "true false false false\n", 6));

    [Theory, ModeData]
    public void PrototypeDefinitionsAcceptOnlyCompatibleDescriptors(ExecutionMode mode)
    {
        Assert.Equal(DefinitionExpected, TestHarness.Run(DefinitionSource, mode));
    }

    internal const string StrictPrototypeWriteSource = """
            "use strict";
            class Ordinary {}
            class Derived extends Ordinary {}
            class Generic<T> {}
            class GenericDerived<T> extends Generic<T> {}
            const expression = class {};
            for (const owner of [Error, TypeError, Ordinary, Derived,
                Generic, GenericDerived, expression]) {
                const alias: any = owner;
                const original = alias.prototype;
                try { alias.prototype = {}; } catch (error) { console.log(error instanceof TypeError); }
                try { alias["prototype"] = original; } catch (error) { console.log(error instanceof TypeError); }
                console.log(alias.prototype === original);
            }
            function ordinaryFunction() {}
            const callable: any = ordinaryFunction;
            const replacement = {};
            callable.prototype = replacement;
            console.log(callable.prototype === replacement);
            const object: any = { prototype: {} };
            object.prototype = replacement;
            console.log(object.prototype === replacement);
            """;

    internal static string StrictPrototypeWriteExpected => string.Concat(Enumerable.Repeat("true\n", 23));

    [Theory, ModeData]
    public void StrictPrototypeWritesRespectConstructorDescriptors(ExecutionMode mode)
    {
        Assert.Equal(StrictPrototypeWriteExpected, TestHarness.Run(StrictPrototypeWriteSource, mode));
    }

    [Theory, ModeData]
    public void ClassMethodPrototypeWritesAreImplicitlyStrict(ExecutionMode mode)
    {
        const string source = """
            class C {
                static write(owner: any) { owner.prototype = {}; }
                static indexed(owner: any) { owner["prototype"] = owner.prototype; }
            }
            class Generic<T> {}
            const expression: any = class {};
            for (const owner of [Error, C, Generic, expression]) {
                try { C.write(owner); } catch (error) { console.log(error instanceof TypeError); }
                try { C.indexed(owner); } catch (error) { console.log(error instanceof TypeError); }
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true\n", 8)), TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void StrictPrototypeDeletesThrowTypeError(ExecutionMode mode)
    {
        const string source = """
            "use strict";
            class Ordinary {}
            class Generic<T> {}
            const expression = class {};
            for (const owner of [Error, TypeError, Ordinary, Generic, expression]) {
                const alias: any = owner;
                const original = alias.prototype;
                try { delete alias.prototype; } catch (error) { console.log(error instanceof TypeError); }
                console.log(alias.prototype === original);
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true\n", 10)), TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void StrictClassExpressionPrototypeWritesThrowTypeError(ExecutionMode mode)
    {
        const string source = """
            "use strict";
            const owner: any = class {};
            const original = owner.prototype;
            try { owner.prototype = {}; } catch (error) { console.log(error instanceof TypeError); }
            try { owner["prototype"] = original; } catch (error) { console.log(error instanceof TypeError); }
            console.log(owner.prototype === original);
            """;
        Assert.Equal("true\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PrototypeContentsAndNormalStaticPropertiesRemainMutable(ExecutionMode mode)
    {
        const string source = """
            class Ordinary {}
            class Generic<T> {}
            function make() { return class {}; }
            for (const owner of [Error, TypeError, Ordinary, Generic, make()]) {
                const alias: any = owner;
                const prototype = alias.prototype;
                prototype.extra = 1;
                Object.defineProperty(prototype, "extra", { value: 2, writable: true, configurable: true });
                prototype.extra = 3;
                console.log(prototype.extra, delete prototype.extra, alias.prototype === prototype);
                alias.extra = 4;
                Object.defineProperty(alias, "extra", { value: 5, writable: true, configurable: true });
                alias.extra = 6;
                console.log(alias.extra, delete alias.extra, Object.hasOwn(alias, "extra"));
                console.log(Reflect.defineProperty(alias, "reflected", {
                    value: 7, writable: true, configurable: true }));
                alias.reflected = 8;
                console.log(alias.reflected, Reflect.deleteProperty(alias, "reflected"),
                    Object.hasOwn(alias, "reflected"));
            }
            function ordinaryFunction() {}
            const callable: any = ordinaryFunction;
            const replacement = {};
            callable.prototype = replacement;
            console.log(callable.prototype === replacement,
                Object.getOwnPropertyDescriptor(callable, "prototype").writable);
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat(
            "3 true true\n6 true false\ntrue\n8 true false\n", 5)) + "true true\n",
            TestHarness.Run(source, mode));
    }
}
