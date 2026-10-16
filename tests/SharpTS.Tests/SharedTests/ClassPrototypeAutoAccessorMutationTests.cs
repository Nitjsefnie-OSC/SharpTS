using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassPrototypeAutoAccessorMutationTests
{
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
