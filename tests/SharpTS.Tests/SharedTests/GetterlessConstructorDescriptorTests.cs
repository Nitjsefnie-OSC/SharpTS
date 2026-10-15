using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GetterlessConstructorDescriptorTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class C {}
            const owner: any = C;
            Object.defineProperty(owner, "value", { set: undefined, configurable: true });
            console.log(owner.value === undefined, owner.value === null);
            const descriptor: any = Object.getOwnPropertyDescriptor(owner, "value");
            console.log(descriptor.get === undefined, descriptor.set === undefined);
            """, "true false\ntrue true\n"];
        yield return ["shapes", """
            class Declared {}
            class Generic<T> {}
            const owners: any[] = [Declared, Generic, class Named {}, class {}];
            const shapes: any[] = [
                { set: undefined }, { set(value: any) {} },
                { get: undefined }, { get: undefined, set: undefined }
            ];
            for (const owner of owners) {
                for (const shape of shapes) {
                    Object.defineProperty(owner, "value", { ...shape, configurable: true });
                    const descriptor: any = Object.getOwnPropertyDescriptor(owner, "value");
                    console.log(owner.value === undefined, owner["value"] === undefined,
                        descriptor.get === undefined, typeof descriptor.set);
                    delete owner.value;
                }
                Object.defineProperty(owner, "value", { value: null, configurable: true });
                console.log(owner.value === null, owner.value === undefined);
                delete owner.value;
                Object.defineProperty(owner, "value", { value: undefined, configurable: true });
                console.log(owner.value === null, owner.value === undefined);
            }
            """, string.Concat(Enumerable.Repeat(
                "true true true undefined\ntrue true true function\ntrue true true undefined\ntrue true true undefined\ntrue false\nfalse true\n", 4))];
        yield return ["inheritance", """
            let reads = 0;
            class Root { static get value(): any { reads++; return "root"; } }
            class Middle extends Root {}
            class Leaf extends Middle {}
            const middle: any = Middle, leaf: any = Leaf;
            Object.defineProperty(middle, "value", { set: undefined, configurable: true });
            console.log(leaf.value === undefined, reads, Object.hasOwn(leaf, "value"));
            delete middle.value;
            console.log(leaf.value, reads);
            class GenericRoot<T> { static get value(): any { reads++; return "generic"; } }
            class GenericMiddle<T> extends GenericRoot<T> {}
            class GenericLeaf<T> extends GenericMiddle<T> {}
            const genericMiddle: any = GenericMiddle, genericLeaf: any = GenericLeaf;
            Object.defineProperty(genericMiddle, "value", { get: undefined, configurable: true });
            console.log(genericLeaf.value === undefined, reads);
            delete genericMiddle.value;
            console.log(genericLeaf.value, reads);
            """, "true 0 false\nroot 1\ntrue 1\ngeneric 2\n"];
        yield return ["callable-and-isolation", """
            let calls = 0;
            class C {}
            const owner: any = C;
            Object.defineProperty(owner, "value", {
                get: function() { calls++; console.log(this === C); return null; }, configurable: true
            });
            console.log(owner.value === null, calls);
            Object.defineProperty(owner, "value", {
                get: function() { calls++; throw new Error("getter"); }, configurable: true
            });
            try { owner.value; } catch (error) { console.log(calls); }
            function make(): any { return class Box {}; }
            const first = make(), second = make();
            Object.defineProperty(first, "value", { set: undefined, configurable: true });
            Object.defineProperty(second, "value", { get: function() { return 7; } });
            console.log(first.value === undefined, second.value, first !== second);
            """, "true\ntrue 1\n2\ntrue 7 true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void GetterlessDescriptorsClaimPropertyAndReturnUndefined(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
