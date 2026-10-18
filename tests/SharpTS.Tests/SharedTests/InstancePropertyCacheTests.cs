using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class InstancePropertyCacheTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class C {}
            const value: any = new C();
            console.log(value.slot === undefined);
            value.slot = 43;
            console.log(value.slot, value['slot'],
                Object.getOwnPropertyDescriptor(value, 'slot')!.value, Object.hasOwn(value, 'slot'));
            """, "true\n43 43 43 true\n"];
        yield return ["strict-dot", """
            function run() {
                'use strict';
                class C {}
                const value: any = new C();
                console.log(value.slot === undefined);
                value.slot = 43;
                console.log(value.slot, value['slot'],
                    Object.getOwnPropertyDescriptor(value, 'slot')!.value, Object.hasOwn(value, 'slot'));
            }
            run();
            """, "true\n43 43 43 true\n"];
        yield return ["presence-miss-and-repeated-writes", """
            class C {}
            const value: any = new C();
            console.log('slot' in value);
            value.slot = 43;
            console.log(value.slot, 'slot' in value, Object.hasOwn(value, 'slot'));
            value.slot = 44;
            console.log(value.slot, value['slot'], Object.getOwnPropertyDescriptor(value, 'slot')!.value);
            """, "false\n43 true true\n44 44 44\n"];
        yield return ["bracket-write-controls", """
            class C {}
            const value: any = new C();
            console.log(value.slot === undefined);
            value['slot'] = 43;
            console.log(value.slot, value['slot']);
            function run() {
                'use strict';
                const value: any = new C();
                console.log(value.slot === undefined);
                value['slot'] = 44;
                console.log(value.slot, value['slot']);
            }
            run();
            """, "true\n43 43\ntrue\n44 44\n"];
        yield return ["cached-method-shadow", """
            class C { read() { return 1; } }
            const value: any = new C();
            const prototypeMethod = value.read;
            console.log(value.read(), Object.hasOwn(value, 'read'));
            value.read = () => 43;
            console.log(value.read(), value['read'](), value.read === prototypeMethod,
                Object.getOwnPropertyDescriptor(value, 'read')!.value === value.read);
            value.read = 44;
            console.log(value.read, value['read']);
            """, "1 false\n43 43 false true\n44 44\n"];
        yield return ["accessor-precedence", """
            const state: any = { seen: 0 };
            class C {
                get slot() { return state.seen; }
                set slot(next: number) { state.seen = next; }
                get fixed() { return 7; }
            }
            const value: any = new C();
            console.log(value.slot, value.fixed);
            value.slot = 43;
            value.fixed = 9;
            console.log(value.slot, value.fixed, Object.hasOwn(value, 'slot'), Object.hasOwn(value, 'fixed'));
            function strictWrite() {
                'use strict';
                try { value.fixed = 9; } catch (error) { console.log('rejected'); }
            }
            strictWrite();
            Object.defineProperty(value, 'extra', {
                get: () => state.seen,
                set: (next: number) => { state.seen = next; }, configurable: true
            });
            console.log(value.extra);
            value.extra = 44;
            console.log(value.extra, state.seen);
            Object.defineProperty(value, 'only', { get: () => 9, configurable: true });
            console.log(value.only);
            value.only = 99;
            console.log(value.only);
            """, "0 7\n43 7 false false\nrejected\n43\n44 44\n9\n9\n"];
        yield return ["blocked-writes", """
            class C { present = 1; }
            const value: any = new C();
            console.log(value.slot === undefined);
            Object.preventExtensions(value);
            value.slot = 43;
            console.log(value.slot === undefined, Object.hasOwn(value, 'slot'));
            function strictAdd(target: any) {
                'use strict';
                try { target.slot = 43; } catch (error) { console.log('rejected'); }
            }
            const frozen: any = Object.freeze(new C());
            frozen.present = 2;
            frozen.slot = 44;
            console.log(frozen.present, frozen.slot === undefined, Object.hasOwn(frozen, 'slot'));
            strictAdd(frozen);
            """, "true\ntrue false\n1 true false\nrejected\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void SuccessfulOwnWritesRefreshCachedPropertyResolutions(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void InterpretedStrictNonExtensibleWritesLeaveTheCachedMissIntact()
    {
        const string source = """
            class C {}
            const value: any = new C();
            console.log(value.slot === undefined);
            Object.preventExtensions(value);
            function run() {
                'use strict';
                for (let index = 0; index < 2; index++) {
                    try { value.slot = 43; } catch (error) { console.log('rejected'); }
                    console.log(value.slot === undefined, Object.hasOwn(value, 'slot'));
                }
            }
            run();
            """;
        Assert.Equal("true\nrejected\ntrue false\nrejected\ntrue false\n",
            TestHarness.RunInterpreted(source));
    }
}
