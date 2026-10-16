using SharpTS.Execution;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GeneratorMethodPrototypeTests
{
    private const string Methods = """
        class C { *g() {} static *sg() {} async *ag() {} static async *sag() {} }
        const E: any = class { *g() {} static *sg() {} async *ag() {} static async *sag() {} };
        const c: any = new C(), e: any = new E(), ctor: any = C;
        const sync: any[] = [c.g, ctor.sg, e.g, E.sg];
        const asynchronous: any[] = [c.ag, ctor.sag, e.ag, E.sag];
        """;

    [Theory, ModeData]
    public void MethodPrototypesHaveStableNonEnumerableNonConfigurableDescriptors(ExecutionMode mode)
    {
        const string source = Methods + """
            for (const method of [...sync, ...asynchronous]) {
                const prototype: any = method.prototype;
                const descriptor: any = Object.getOwnPropertyDescriptor(method, "prototype");
                console.log(Object.hasOwn(method, "prototype"), typeof prototype,
                    descriptor.value === prototype, descriptor.writable,
                    descriptor.enumerable, descriptor.configurable);
                console.log(Reflect.deleteProperty(method, "prototype"), method.prototype === prototype);
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true object true true false false\nfalse true\n", 8)),
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void MethodsAndIteratorsKeepTheirDistinctIntrinsicChains(ExecutionMode mode)
    {
        const string source = Methods + """
            for (const list of [sync, asynchronous]) {
                for (const method of list) {
                    const own: any = method.prototype, intrinsic: any = Object.getPrototypeOf(own);
                    const functionIntrinsic: any = Object.getPrototypeOf(method);
                    const iterator: any = method();
                    console.log(Reflect.ownKeys(own).length, Object.getPrototypeOf(iterator) === own,
                        functionIntrinsic.prototype === intrinsic,
                        intrinsic.constructor === functionIntrinsic,
                        Object.getPrototypeOf(functionIntrinsic) === Function.prototype, iterator instanceof method);
                    console.log(Object.getPrototypeOf(own) === Object.getPrototypeOf(list[0].prototype),
                        iterator.next === intrinsic.next, iterator.return === intrinsic.return);
                    console.log(Reflect.getPrototypeOf(method) === functionIntrinsic,
                        Reflect.getPrototypeOf(iterator) === own,
                        Object.getOwnPropertyDescriptor(method, "constructor") === undefined);
                    const constructor: any = Object.getOwnPropertyDescriptor(functionIntrinsic, "constructor");
                    const prototype: any = Object.getOwnPropertyDescriptor(functionIntrinsic, "prototype");
                    const tag: any = Object.getOwnPropertyDescriptor(intrinsic, Symbol.toStringTag);
                    console.log(constructor.value === functionIntrinsic.constructor,
                        constructor.writable, constructor.enumerable, constructor.configurable,
                        prototype.writable, prototype.enumerable, prototype.configurable,
                        tag.writable, tag.enumerable, tag.configurable);
                    console.log(functionIntrinsic.constructor.name, functionIntrinsic[Symbol.toStringTag],
                        intrinsic[Symbol.toStringTag], Object.prototype.toString.call(method),
                        Object.prototype.toString.call(iterator));
                    const constructorPrototype: any = Object.getOwnPropertyDescriptor(functionIntrinsic.constructor, "prototype");
                    console.log(functionIntrinsic.constructor.prototype === functionIntrinsic,
                        constructorPrototype.value === functionIntrinsic, constructorPrototype.writable,
                        constructorPrototype.enumerable, constructorPrototype.configurable,
                        functionIntrinsic.constructor.length);
                }
            }
            console.log(sync[0].prototype !== sync[1].prototype,
                Object.getPrototypeOf(sync[0].prototype) !== Object.getPrototypeOf(asynchronous[0].prototype));
            console.log(c.g.prototype === ctor.prototype.g.prototype,
                e.g.prototype === E.prototype.g.prototype, c.ag.prototype === ctor.prototype.ag.prototype);
            """;
        const string common = "0 true true true true true\ntrue true true\ntrue true true\ntrue false false true false false true false false true\n";
        string expected = string.Concat(Enumerable.Repeat(common +
            "GeneratorFunction GeneratorFunction Generator [object GeneratorFunction] [object Generator]\ntrue true false false false 1\n", 4))
            + string.Concat(Enumerable.Repeat(common +
            "AsyncGeneratorFunction AsyncGeneratorFunction AsyncGenerator [object AsyncGeneratorFunction] [object AsyncGenerator]\ntrue true false false false 1\n", 4))
            + "true true\ntrue true true\n";
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PrototypeWritesAndIntegrityApplyAcrossReceiverViews(ExecutionMode mode)
    {
        const string source = Methods + """
            for (const method of [c.g, c.ag]) {
                const original: any = method.prototype, custom: any = {};
                method.prototype = custom;
                console.log(Object.getPrototypeOf(method()) === custom);
                method["prototype"] = 42;
                console.log(Object.getPrototypeOf(method()) === Object.getPrototypeOf(original));
                try { console.log(method() instanceof method); }
                catch (error) { console.log(error instanceof TypeError, 1 instanceof method); }
                Object.defineProperty(method, "prototype", { value: custom, writable: false });
                console.log(method.prototype === custom,
                    Reflect.defineProperty(method, "prototype", { value: {} }));
                try { new method(); console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            console.log(c.g.prototype === ctor.prototype.g.prototype,
                c.ag.prototype === ctor.prototype.ag.prototype);
            const frozen: any = ctor.sg;
            Object.freeze(frozen);
            const frozenDescriptor: any = Object.getOwnPropertyDescriptor(frozen, "prototype");
            console.log(frozenDescriptor.writable,
                Reflect.defineProperty(frozen, "prototype", { value: {} }));
            """;
        Assert.Equal("true\ntrue\ntrue false\ntrue false\ntrue\ntrue\ntrue\ntrue false\ntrue false\ntrue\ntrue true\nfalse false\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void DeclarationAndUnliftedExpressionWrappersUseTheSamePrototypeContract(ExecutionMode mode)
    {
        const string source = """
            function* declared() {}
            async function* asynchronous() {}
            const functions: any[] = [declared, asynchronous];
            { let local = 1;
                functions.push(function*() { yield local; });
                functions.push(async function*() { yield local; });
            }
            for (const fn of functions) {
                const descriptor: any = Object.getOwnPropertyDescriptor(fn, "prototype");
                console.log(Object.hasOwn(fn, "prototype"), descriptor.writable,
                    descriptor.enumerable, descriptor.configurable,
                    Object.getPrototypeOf(fn()) === fn.prototype);
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true true false false true\n", 4)),
            TestHarness.Run(source, mode));
    }

    [Fact]
    public void GeneratorIntrinsicsAreSeparateForEachInterpreterRealm()
    {
        using var first = new Interpreter(stdout: TextWriter.Null, stderr: TextWriter.Null);
        using var second = new Interpreter(stdout: TextWriter.Null, stderr: TextWriter.Null);
        var left = first.GetGeneratorIntrinsics(false);
        var right = second.GetGeneratorIntrinsics(false);
        Assert.NotSame(left.FunctionPrototype, right.FunctionPrototype);
        Assert.NotSame(left.GeneratorPrototype, right.GeneratorPrototype);
        Assert.Same(first.GetFunctionPrototype(), left.FunctionPrototype.Prototype);
        Assert.Same(left.GeneratorPrototype, left.FunctionPrototype.GetProperty("prototype"));
        Assert.NotSame(left.GeneratorPrototype, first.GetGeneratorIntrinsics(true).GeneratorPrototype);
    }

    [Fact]
    public void IntrinsicZeroArgumentConstructorsCreateAnonymousGenerators()
    {
        const string source = Methods + """
            for (const method of [c.g, c.ag]) {
                const constructor: any = Object.getPrototypeOf(method).constructor;
                const generated: any = constructor();
                console.log(generated.name, generated instanceof constructor,
                    Object.getPrototypeOf(generated) === constructor.prototype,
                    Object.getPrototypeOf(generated()) === generated.prototype);
                try { new generated(); console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            """;
        Assert.Equal("anonymous true true true\ntrue\nanonymous true true true\ntrue\n",
            TestHarness.Run(source, ExecutionMode.Interpreted));
    }

    [Fact]
    public void TransferredMethodsKeepTheRealmInWhichTheyWereCreated()
    {
        var files = new Dictionary<string, string> { ["main.ts"] = """
            import * as vm from 'vm';
            class Local { *g() {} async *ag() {} }
            const local: any = new Local();
            const remote: any = vm.runInNewContext('class C { *g() {} async *ag() {} }; new C()');
            for (const pair of [[remote.g, local.g], [remote.ag, local.ag]]) {
                const functionPrototype: any = Object.getPrototypeOf(pair[0]);
                console.log(functionPrototype !== Object.getPrototypeOf(pair[1]),
                    Object.getPrototypeOf(pair[0].prototype) !== Object.getPrototypeOf(pair[1].prototype));
                const generated: any = functionPrototype.constructor();
                console.log(Object.getPrototypeOf(generated) === functionPrototype,
                    Object.getPrototypeOf(generated.prototype) === Object.getPrototypeOf(pair[0].prototype));
            }
            """ };
        Assert.Equal("true true\ntrue true\ntrue true\ntrue true\n",
            TestHarness.RunModules(files, "main.ts", ExecutionMode.Interpreted));
    }

    [Fact]
    public void SharedPropertyBagsPreserveEnumerableAndSymbolDescriptorOperations()
    {
        const string source = Methods + """
            for (const method of [c.g, c.ag]) {
                method.extra = 3;
                const symbol = Symbol("extra");
                method[symbol] = 4;
                console.log(Object.keys(method).join(","), Object.values(method).join(","), Object.entries(method)[0].join(":"));
                const copied: any = Object.assign({}, method);
                const descriptor: any = Object.getOwnPropertyDescriptor(method, symbol);
                console.log(copied.extra === 3, copied[symbol] === 4,
                    Object.getOwnPropertySymbols(method)[0] === symbol,
                    descriptor.value === 4);
                delete method[symbol];
                console.log(Object.getOwnPropertySymbols(method).length, Object.hasOwn(method, symbol));
                Object.defineProperty(method, symbol, { value: 5, enumerable: true });
                const fixed: any = Reflect.getOwnPropertyDescriptor(method, symbol);
                console.log(fixed.value === 5,
                    Reflect.deleteProperty(method, symbol), Object.hasOwn(method, symbol));
                const setter = Symbol("setter");
                Object.defineProperty(method, setter, { set(value: any) { console.log(this === method, value); } });
                method[setter] = 9;
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("extra 3 extra:3\ntrue true true true\n0 false\ntrue false true\ntrue 9\n", 2)),
            TestHarness.Run(source, ExecutionMode.Interpreted));
    }

    [Fact]
    public void PrimitiveValuesDoNotAcquireGeneratorInstanceIdentityThroughBoxing()
    {
        const string source = Methods + """
            for (const method of [c.g, c.ag]) {
                method.prototype = Number.prototype;
                console.log(1 instanceof method, NaN instanceof method);
                method.prototype = String.prototype;
                console.log("x" instanceof method, "" instanceof method);
                method.prototype = Boolean.prototype;
                console.log(true instanceof method, false instanceof method);
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("false false\n", 6)),
            TestHarness.Run(source, ExecutionMode.Interpreted));
    }
}
