using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionAutoAccessorTests
{
    // #2521 tracks compiled expression auto-accessors; any receivers bypass checker issue #2280.
    [Fact]
    public void ExpressionsRetainBackingStorageAndPrototypeDescriptors()
    {
        const string source = """
            const C = class Named {
                accessor value: string = "x";
                static accessor count: number = 4;
            };
            const first: any = new C(), second: any = new C(), owner: any = C;
            console.log(first.value);
            first.value = "changed";
            console.log(first.value, second.value, Object.hasOwn(first, 'value'));
            const descriptor: any = Object.getOwnPropertyDescriptor(C.prototype, 'value');
            console.log(typeof descriptor.get, typeof descriptor.set, descriptor.enumerable, descriptor.configurable);
            console.log(descriptor.get.call(first));
            descriptor.set.call(second, 'second');
            console.log(first.value, second.value);
            try { descriptor.get.call({}); } catch (error) { console.log(error instanceof TypeError); }
            console.log(owner.count);
            owner.count = 5;
            console.log(owner.count);
            """;
        Assert.Equal("x\nchanged x false\nfunction function false true\nchanged\nchanged second\ntrue\n4\n5\n",
            TestHarness.RunInterpreted(source));
    }

    [Fact]
    public void NativeBackedExpressionsInitializeAutoAccessorBrands()
    {
        const string source = """
            const ArrayBox = class extends Array { accessor value: number = 1; };
            const ErrorBox = class extends Error { accessor value: number = 2; };
            const PromiseBox = class extends Promise<any> {
                accessor value: number = 3;
                constructor() { super((resolve: any) => resolve(0)); }
            };
            for (const Owner of [ArrayBox, ErrorBox, PromiseBox]) {
                const instance: any = new (Owner as any)();
                const descriptor: any = Object.getOwnPropertyDescriptor(Owner.prototype, 'value');
                console.log(descriptor.get.call(instance), Object.hasOwn(instance, 'value'));
                descriptor.set.call(instance, 9);
                console.log(descriptor.get.call(instance));
            }
            """;
        Assert.Equal("1 false\n9\n2 false\n9\n3 false\n9\n", TestHarness.RunInterpreted(source));
    }

    [Fact]
    public void NamedExpressionInitializersRetainTheirLexicalClassBinding()
    {
        const string source = """
            const C = class Named { accessor value: any = Named; };
            class Child extends C {}
            const first: any = new Child(), second: any = new Child();
            console.log(first.value === C, second.value === C);
            first.value = 7;
            console.log(first.value, second.value === C);
            """;
        Assert.Equal("true true\n7 true\n", TestHarness.RunInterpreted(source));
    }
}
