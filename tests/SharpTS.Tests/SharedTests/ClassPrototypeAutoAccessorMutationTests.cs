using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassPrototypeAutoAccessorMutationTests
{
    // #2570 tracks the independently verified compiled descriptor omission.
    [Theory, InterpretedOnlyData]
    public void AutoAccessorDescriptorsExposeCallableBrandedAccessors(ExecutionMode mode)
    {
        const string source = """
            class C { accessor label: string = 'first'; }
            const descriptor: any = Object.getOwnPropertyDescriptor(C.prototype, 'label');
            console.log(typeof descriptor, typeof descriptor?.get, typeof descriptor?.set);
            const first: any = new C(), second: any = new C();
            console.log(descriptor.enumerable, descriptor.configurable, Object.hasOwn(descriptor, 'value'),
                Object.hasOwn(first, 'label'));
            console.log(descriptor.get.call(first), descriptor.get.call(second));
            descriptor.set.call(first, 'changed');
            console.log(first.label, second.label);
            try { descriptor.get.call({}); } catch (error) { console.log(error instanceof TypeError); }
            try { console.log((Object.create(C.prototype) as any).label); }
            catch (error) { console.log(error instanceof TypeError); }
            """;
        Assert.Equal("object function function\nfalse true false false\nfirst first\nchanged first\ntrue\ntrue\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void OwnAutoAccessorSurvivesParentReplacementAndRemovedAncestorIsAbsent(ExecutionMode mode)
    {
        const string source = """
            class A { accessor inherited: number = 5; }
            class B extends A { accessor amount: number = 1; }
            const instance: any = new B(), home: any = B.prototype;
            Object.setPrototypeOf(home, { marker: 'switched' });
            console.log(instance.amount, instance.inherited === undefined, Reflect.get(instance, 'amount'));
            instance.amount = 7;
            console.log(instance.amount);
            Object.setPrototypeOf(home, null);
            console.log(instance.amount, instance.inherited === undefined, Reflect.get(instance, 'amount'));
            instance.amount = 9;
            console.log(instance.amount);
            """;
        const string expected = "1 true 1\n7\n7 true 7\n9\n";
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
