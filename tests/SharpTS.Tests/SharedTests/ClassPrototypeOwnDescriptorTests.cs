using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassPrototypeOwnDescriptorTests
{
    [Theory, ModeData]
    public void OwnReflectionDoesNotReadInheritedClassPrototypeGetters(ExecutionMode mode)
    {
        const string source = """
            let reads = 0;
            class Base {
                get value() { reads++; return 1; }
                static get value() { reads++; return 2; }
                method() { return 3; }
            }
            class Child extends Base {}
            console.log(Object.getOwnPropertyDescriptor(Child.prototype, 'value') === undefined,
                Object.getOwnPropertyDescriptor(Child, 'value') === undefined,
                Object.hasOwn(Child.prototype, 'value'), Object.hasOwn(Child, 'value'), reads);
            Object.setPrototypeOf(Child.prototype, { get value() { reads++; return 4; } });
            console.log(Object.getOwnPropertyDescriptor(Child.prototype, 'value') === undefined,
                Reflect.getOwnPropertyDescriptor(Child.prototype, 'value') === undefined,
                Object.hasOwn(Child.prototype, 'value'),
                Object.prototype.hasOwnProperty.call(Child.prototype, 'value'), reads);
            Object.setPrototypeOf(Child.prototype, null);
            console.log(Object.getOwnPropertyDescriptor(Child.prototype, 'value') === undefined, reads);
            const accessor: any = Object.getOwnPropertyDescriptor(Base.prototype, 'value');
            const repeatedAccessor: any = Object.getOwnPropertyDescriptor(Base.prototype, 'value');
            const constructor: any = Object.getOwnPropertyDescriptor(Base.prototype, 'constructor');
            const method: any = Object.getOwnPropertyDescriptor(Base.prototype, 'method');
            console.log(typeof accessor.get,
                accessor.get === repeatedAccessor.get,
                constructor.value === Base, typeof method.value,
                method.writable, method.enumerable, method.configurable, reads);
            """;
        Assert.Equal("true true false false 0\ntrue true false false 0\ntrue 0\nfunction true true function true false true 0\n",
            TestHarness.Run(source, mode));
    }
}
