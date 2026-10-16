using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class MethodValueAsyncArrowTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["generic-super-capture", """
            class A<T> { value(x: T): T { return x; } }
            class B<T> extends A<T> {
                read(x: T) {
                    const callback = async () => {
                        await Promise.resolve(0);
                        const method = super.value;
                        return method(x);
                    };
                    return callback();
                }
            }
            new B<string>().read('ok').then(value => console.log(value));
            """, "ok\n"];
        yield return ["extracted-method-captures-borrowed-receiver", """
            class Values {
                value = 'original';
                make(prefix: string) {
                    return async () => {
                        await Promise.resolve(0);
                        return prefix + ':' + this.value;
                    };
                }
            }
            const instance: any = new Values();
            const make: any = instance.make;
            const receiver: any = { value: 'borrowed' };
            const read = make.call(receiver, 'prefix');
            receiver.value = 'updated';
            async function run() {
                console.log(await read());
                console.log(await make.call(instance, 'own')());
            }
            run();
            """, "prefix:updated\nown:original\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void MethodValuesCreateAsyncArrowsWithTheirLexicalCaptures(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
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
