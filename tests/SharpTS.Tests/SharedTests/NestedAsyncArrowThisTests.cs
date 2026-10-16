using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class NestedAsyncArrowThisTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["three-level-method", """
            class C {
                value = 5;
                async run() {
                    const first = async () => {
                        const second = async () => {
                            const third = async () => { await Promise.resolve(0); return this.value; };
                            return await third();
                        };
                        return await second();
                    };
                    return await first();
                }
            }
            async function check() {
                console.log(await new C().run());
                const borrowed: any = new C().run;
                console.log(await borrowed.call({ value: 9 }));
            }
            check().catch(error => console.log(error.name, error.message));
            """, "5\n9\n"];
        yield return ["returned-arrows-ignore-rebinding", """
            class C {
                value = 7;
                async make() {
                    return async () => {
                        return async () => { await Promise.resolve(0); return this.value; };
                    };
                }
            }
            async function check() {
                const first: any = await new C().make();
                const second: any = await first.call({ value: -1 });
                console.log(await second.call({ value: -2 }));
                const borrowed: any = new C().make;
                const other: any = await borrowed.call({ value: 11 });
                const last: any = await other.call({ value: -3 });
                console.log(await last.call({ value: -4 }));
            }
            check().catch(error => console.log(error.name, error.message));
            """, "7\n11\n"];
        yield return ["sync-arrow-in-nested-async-arrow", """
            class C {
                value = 13;
                async run() {
                    const first = async () => {
                        const second = async () => {
                            await Promise.resolve(0);
                            return (() => this.value)();
                        };
                        return await second();
                    };
                    return await first();
                }
            }
            const borrowed: any = new C().run;
            borrowed.call({ value: 17 }).then((value: any) => console.log(value))
                .catch((error: any) => console.log(error.name, error.message));
            """, "17\n"];
        yield return ["ordinary-async-function-receiver", """
            async function run() {
                const first = async () => {
                    const second = async () => { await Promise.resolve(0); return this.value; };
                    return await second();
                };
                return await first();
            }
            const invoke: any = run;
            invoke.call({ value: 19 }).then((value: any) => console.log(value))
                .catch((error: any) => console.log(error.name, error.message));
            """, "19\n"];
        yield return ["generic-owner-with-sync-arrow", """
            class C<T> {
                value: T;
                constructor(value: T) { this.value = value; }
                async run() {
                    const first = async () => {
                        const second = async () => {
                            await Promise.resolve(0);
                            return (() => this.value)();
                        };
                        return await second();
                    };
                    return await first();
                }
            }
            async function check() {
                console.log(await new C<number>(23).run());
                const borrowed: any = new C<string>('nominal').run;
                console.log(await borrowed.call({ value: 'guest' }));
            }
            check().catch(error => console.log(error.name, error.message));
            """, "23\nguest\n"];
        yield return ["private-brand-with-sync-arrow", """
            class C {
                #value = 0;
                constructor(value: number) { this.#value = value; }
                async run() {
                    const first = async () => {
                        const second = async () => {
                            await Promise.resolve(0);
                            return (() => this.#value)();
                        };
                        return await second();
                    };
                    return await first();
                }
            }
            async function check() {
                const first = new C(31), second = new C(37);
                console.log(await first.run());
                const borrowed: any = first.run;
                console.log(await borrowed.call(second));
                try { await borrowed.call({ value: 41 }); }
                catch (error: any) { console.log(error instanceof TypeError); }
            }
            check().catch(error => console.log(error.name, error.message));
            """, "31\n37\ntrue\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void NestedArrowsRetainTheirLexicalReceiver(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
