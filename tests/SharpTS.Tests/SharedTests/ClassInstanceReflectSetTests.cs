using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassInstanceReflectSetTests
{
    [Theory, ModeData]
    public void RepeatedWritesPreserveExistingFieldAttributes(ExecutionMode mode)
    {
        const string source = """
            class C { value = 1; }
            const instance: any = new C();
            console.log(Reflect.set(instance, 'value', 7), instance.value);
            console.log(Reflect.set(instance, 'value', 9), instance.value);
            const descriptor: any = Object.getOwnPropertyDescriptor(instance, 'value');
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(Reflect.set(instance, 'extra', 8), instance.extra);
            """;
        Assert.Equal("true 7\ntrue 9\n9 true true true\ntrue 8\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void SealedWritableFieldsAcceptRepeatedReflectUpdates(ExecutionMode mode)
    {
        const string source = """
            class C { value = 1; }
            const instance: any = new C();
            Object.seal(instance);
            console.log(Reflect.set(instance, 'value', 2), Reflect.set(instance, 'value', 3));
            const sealed: any = Object.getOwnPropertyDescriptor(instance, 'value');
            console.log(sealed.value, sealed.writable, sealed.enumerable, sealed.configurable);
            console.log(Reflect.set(instance, 'missing', 4));
            Object.freeze(instance);
            console.log(Reflect.set(instance, 'value', 5), instance.value);
            """;
        Assert.Equal("true true\n3 true true false\nfalse\nfalse 3\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PartialDefinitionsRetainValuesAttributesAndAccessorKind(ExecutionMode mode)
    {
        const string source = """
            class C { value: any = 1; }
            const instance: any = new C();
            Object.defineProperty(instance, 'value', { value: 5 });
            Object.defineProperty(instance, 'value', { enumerable: false });
            const descriptor: any = Object.getOwnPropertyDescriptor(instance, 'value');
            console.log(instance.value === 5, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            Object.defineProperty(instance, 'value', { value: undefined });
            console.log(instance.value === undefined, Reflect.set(instance, 'value', 7), instance.value);
            Object.defineProperty(instance, 'accessor', { get: function() { return 8; }, configurable: true });
            Object.defineProperty(instance, 'accessor', { enumerable: true });
            const accessor: any = Object.getOwnPropertyDescriptor(instance, 'accessor');
            console.log(instance.accessor, typeof accessor.get, accessor.enumerable, Object.hasOwn(accessor, 'value'));
            """;
        Assert.Equal("true true false true\ntrue true 7\n8 function true false\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void NullValuesSurviveAttributeOnlyDefinitions()
    {
        // #2571 tracks compiled null loss during attribute-only redefinition.
        const string source = """
            class C { value: any = 1; }
            const instance: any = new C();
            Object.defineProperty(instance, 'value', { value: null });
            Object.defineProperty(instance, 'value', { enumerable: false });
            const descriptor: any = Object.getOwnPropertyDescriptor(instance, 'value');
            console.log(instance.value === null, descriptor.value === null,
                descriptor.writable, descriptor.enumerable, descriptor.configurable);
            """;
        Assert.Equal("true true true false true\n", TestHarness.RunInterpreted(source));
    }
}
