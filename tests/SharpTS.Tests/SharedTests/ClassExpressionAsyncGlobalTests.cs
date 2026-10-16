using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassExpressionAsyncGlobalTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class A { value(): any { return 'declared'; } }
            const B: any = class extends A {
                label = 'child';
                make() { return async () => { await Promise.resolve(0); return this.label; }; }
            };
            const base: any = A.prototype;
            Object.defineProperty(base, 'value', { get() { return this.label; }, configurable: true });
            const read: any = new B().make();
            read.call({ label: 'fake' }).then((value: any) => console.log(value))
                .catch((error: any) => console.log(error.name, error.message));
            """, "child\n"];
        yield return ["without-prototype-observation", """
            class A { value(): any { return 'declared'; } }
            const B: any = class extends A {
                label = 'child';
                make() { return async () => { await Promise.resolve(0); return this.label; }; }
            };
            const read: any = new B().make();
            read.call({ label: 'fake' }).then(value => console.log(value));
            """, "child\n"];
        yield return ["method-local-capture-and-builtins", """
            class A {}
            const B: any = class extends A {
                label = 'child';
                make(prefix: string) {
                    return async () => {
                        await Promise.resolve(0);
                        const math: any = Math, json: any = JSON;
                        return prefix + ':' + this.label + ':' + math.max(1, 2) + ':'
                            + (json.stringify({ x: 1 }) === '{"x":1}') + ':' + Object.keys({ x: 1 })[0];
                    };
                }
            };
            Object.defineProperty(A.prototype, 'unused', { value: 1 });
            const read: any = new B().make('prefix');
            read().then(value => console.log(value))
                .catch(error => console.log(error.name, error.message));
            """, "prefix:child:2:true:x\n"];
        yield return ["method-shadowed-promise", """
            const B: any = class {
                label = 'method';
                make(Promise: any) {
                    return async () => {
                        const local: any = Promise;
                        return this.label + ':' + await local.resolve(7);
                    };
                }
            };
            const read: any = new B().make({ resolve(value: number) { return value + 2; } });
            read().then(value => console.log(value));
            """, "method:9\n"];
        yield return ["separate-class-evaluations", """
            class A {}
            function make(label: string) {
                return class extends A {
                    tag = label;
                    read() { return async () => { await Promise.resolve(0); return this.tag; }; }
                };
            }
            Object.defineProperty(A.prototype, 'unused', { value: 1 });
            const First: any = make('first'), Second: any = make('second');
            async function run() {
                console.log(await new First().read()());
                console.log(await new Second().read()());
            }
            run();
            """, "first\nsecond\n"];
        yield return ["nested-async-arrows", """
            class A {}
            const B: any = class extends A {
                label = 'nested';
                make() {
                    return async () => {
                        await Promise.resolve(0);
                        const inner = async () => { await Promise.resolve(0); return this.label; };
                        return await inner();
                    };
                }
            };
            Object.defineProperty(A.prototype, 'unused', { value: 1 });
            new B().make()().then(value => console.log(value));
            """, "nested\n"];
        yield return ["standalone-async-arrow-control", """
            class A {}
            Object.defineProperty(A.prototype, 'unused', { value: 1 });
            const read = async () => { await Promise.resolve(0); return 'async'; };
            read().then(value => console.log(value));
            """, "async\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AsyncArrowsPreserveReceiversLocalsAndGlobalBindings(
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
