using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class CallableFunctionPrototypeTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["calls-and-wrappers", """
            const prototype: any = Function.prototype;
            console.log(typeof prototype, prototype() === undefined, prototype(1, 2) === undefined);
            console.log(prototype.call({}) === undefined, prototype.apply({}, [1]) === undefined,
                prototype.bind({})(3) === undefined);
            console.log(Reflect.apply(prototype, {}, []) === undefined);
            """, "function true true\ntrue true true\ntrue\n"];
        yield return ["callbacks", """
            const prototype: any = Function.prototype;
            console.log([1, 2].map(prototype).every((value: any) => value === undefined));
            console.log(Array.from([1, 2], prototype).every((value: any) => value === undefined));
            """, "true\ntrue\n"];
        yield return ["proxy", """
            const prototype: any = Function.prototype, proxy: any = new Proxy(prototype, {});
            console.log(typeof proxy, proxy() === undefined);
            try { new proxy(); } catch (error) { console.log(error instanceof TypeError); }
            const root: any = { items: [{ value: 1 }] }, receivers: any[] = [];
            const replacer: any = new Proxy(prototype, {
                apply(target: any, holder: any, args: any[]) {
                    const key = args[0];
                    if (key === "") receivers.push(holder[""] === root);
                    if (key === "items") receivers.push(holder === root);
                    if (key === "0") receivers.push(holder === root.items);
                    if (key === "value") receivers.push(holder === root.items[0]);
                    return args[1];
                }
            });
            console.log(JSON.stringify(root, replacer));
            console.log(receivers.join(","));
            """, "function true\ntrue\n{\"items\":[{\"value\":1}]}\ntrue,true,true,true\n"];
        yield return ["ordinary-has-instance-order", """
            const prototype: any = Function.prototype, ordinary: any = prototype[Symbol.hasInstance];
            console.log(ordinary.call(prototype, 1), ordinary.call(prototype, null));
            try { ordinary.call(prototype, {}); } catch (error) { console.log(error instanceof TypeError); }
            let reads = 0;
            Object.defineProperty(prototype, "prototype", {
                get() { reads++; return 1; }, configurable: true
            });
            console.log(ordinary.call(prototype, 1), reads);
            try { ordinary.call(prototype, {}); } catch (error) { console.log(error instanceof TypeError, reads); }
            const parent = {};
            Object.defineProperty(prototype, "prototype", { value: parent, configurable: true });
            console.log(ordinary.call(prototype, Object.create(parent)), ordinary.call(prototype, {}));
            """, "false false\ntrue\nfalse 0\ntrue 1\ntrue false\n"];
        yield return ["nonconstructible-and-metadata", """
            const prototype: any = Function.prototype, bound: any = prototype.bind({});
            function rejects(value: any) {
                try { new value(); return false; } catch (error) { return error instanceof TypeError; }
            }
            console.log(rejects(prototype), rejects(bound));
            try { Reflect.construct(prototype, []); } catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.construct(Object, [], prototype); } catch (error) { console.log(error instanceof TypeError); }
            console.log(prototype.name === "", prototype.length === 0, Object.hasOwn(prototype, "prototype"));
            Object.defineProperty(prototype, "name", { value: "changed", configurable: true });
            console.log(prototype.name, typeof prototype, prototype() === undefined);
            """, "true true\ntrue\ntrue\ntrue true false\nchanged function true\n"];
        yield return ["serialization-and-accessors", """
            const prototype: any = Function.prototype, object: any = {};
            console.log(JSON.stringify(prototype) === undefined, JSON.stringify({ fn: prototype }),
                JSON.stringify([prototype]));
            console.log(JSON.stringify({ value: 1 }, prototype) === undefined,
                Object.prototype.toString.call(prototype));
            Object.defineProperty(object, "value", { get: prototype });
            Object.defineProperty(object, "direct", { set: prototype });
            object.__defineGetter__("getter", prototype);
            object.__defineSetter__("setter", prototype);
            console.log(object.value === undefined, object.getter === undefined,
                typeof Object.getOwnPropertyDescriptor(object, "setter")!.set);
            object.direct = 1; object.setter = 2;
            console.log("done");
            """, "true {} [null]\ntrue [object Function]\ntrue true function\ndone\n"];
        yield return ["grouping-and-promise-callbacks", """
            const prototype: any = Function.prototype;
            console.log(Object.keys(Object.groupBy([], prototype)).length);
            const catchMethod: any = Promise.prototype.catch, finallyMethod: any = Promise.prototype.finally;
            console.log(catchMethod.call({ then: prototype }, () => 0) === undefined,
                finallyMethod.call({ then: prototype }, () => 0) === undefined);
            async function run() {
                console.log(await Promise.resolve(1).then(prototype) === undefined);
                console.log(await Promise.reject(1).catch(prototype) === undefined);
            }
            run();
            """, "0\ntrue true\ntrue\ntrue\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void FunctionPrototypeIsCallableWithOrdinaryCallableSemantics(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
