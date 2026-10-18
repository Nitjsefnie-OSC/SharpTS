using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionReflectDefinitionTests
{
    // Compiled sealed/nonextensible function prototype writes are #2330.
    [Fact]
    public void SealedAndNonextensibleFunctionsRetainWritablePrototypeUpdates()
    {
        const string source = """
            function sealed() {}
            function prevented() {}
            function* generator() {}
            const expression = function() {};
            const targets: any[] = [sealed, prevented, expression, generator];
            for (let index = 0; index < targets.length; index++) {
                const target = targets[index];
                if (index % 2 === 0) Object.seal(target);
                else Object.preventExtensions(target);
                const before: any = Object.getOwnPropertyDescriptor(target, 'prototype'), first = {}, second = {};
                console.log(before.writable, before.enumerable, before.configurable);
                console.log(Reflect.set(target, 'prototype', first), target.prototype === first);
                console.log(Reflect.defineProperty(target, 'prototype', { value: second }), target.prototype === second);
                console.log(Reflect.defineProperty(target, 'newProperty', { value: 1 }), Object.hasOwn(target, 'newProperty'));
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true false false\ntrue true\ntrue true\nfalse false\n", 4)),
            TestHarness.Run(source, ExecutionMode.Interpreted));
    }

    [Theory, ModeData]
    public void OrdinaryCallableKindsUseOwnDescriptorStorage(ExecutionMode mode)
    {
        const string source = """
            function declaration() {}
            const expression = function() {}, arrow = () => 0, bound = declaration.bind(null);
            function* generator() {}
            for (const target of [declaration, expression, arrow, bound, generator] as any[]) {
                console.log(Reflect.defineProperty(target, 'extra', { value: 7, writable: true, configurable: true }));
                console.log(Reflect.defineProperty(target, 'extra', { enumerable: true }));
                const descriptor: any = Object.getOwnPropertyDescriptor(target, 'extra');
                console.log(target.extra, descriptor.writable, descriptor.enumerable, descriptor.configurable);
                Object.freeze(target);
                console.log(Reflect.defineProperty(target, 'extra', { value: 8 }), target.extra,
                    Reflect.defineProperty(target, 'newProperty', { value: 1 }));
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true\ntrue\n7 true true true\nfalse 7 false\n", 5)),
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void CallableKeysAreCoercedBeforeDescriptorGetters(ExecutionMode mode)
    {
        const string source = """
            function target() {}
            const key = Symbol('key'), state = { events: '' };
            const name: any = { [Symbol.toPrimitive]() { state.events += 'k'; return key; } };
            const descriptor: any = { get value() { state.events += 'v'; return 7; } };
            console.log(Reflect.defineProperty(target, name, descriptor), (target as any)[key], state.events);
            """;
        Assert.Equal("true 7 kv\n", TestHarness.Run(source, mode));
    }
}
