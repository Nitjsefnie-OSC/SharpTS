using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ConstructorNullArgumentTests
{
    internal const string CaptureSource = """
        class C {
            read: any;
            constructor(value: number, flag: boolean) {
                const stable: number = 43;
                const iterator = { next: () => [value, flag, typeof value, typeof flag, stable] };
                this.read = iterator.next;
                console.log("raw", value, flag, typeof value, typeof flag);
            }
        }
        const bind: any = (() => 0).bind;
        const Bound: any = bind.call(C, {}, null, null);
        const value: any = new Bound();
        const captured: any = value.read();
        console.log("capture", captured[0], captured[1], captured[2], captured[3], captured[4]);
        const control: any = new C(7, false);
        const capturedControl: any = control.read();
        console.log("capture", capturedControl[0], capturedControl[1], capturedControl[2], capturedControl[3], capturedControl[4]);
        """;

    internal const string OrdinaryCaptureSource = """
        function constructor(value: number) {
            const stable: number = 45;
            const iterator = { next: () => [value, typeof value, stable] };
            return iterator;
        }
        const ordinary: any = constructor(8).next();
        console.log(ordinary[0], ordinary[1], ordinary[2]);
        const second: any = constructor(9).next();
        console.log(second[0], second[1], second[2]);
        """;

    internal const string ExpressionCaptureProofSource = """
        const E = class {
            read: any;
            constructor(value: number) {
                const stable: number = 44;
                const iterator = { next: () => [value, typeof value, stable] };
                this.read = iterator.next;
            }
        };
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class C { constructor(value: number, flag: boolean) { console.log(value, flag); } }
            const extractedBind: any = (() => 0).bind;
            const Bound: any = extractedBind.call(C, {}, null, null);
            new Bound();
            """, "null null\n"];
        yield return ["boundaries", """
            class C {
                constructor(value: number, flag: boolean) {
                    console.log(value, flag, typeof value, typeof flag, arguments.length, arguments[0], arguments[1]);
                }
            }
            new C(null as any, null as any);
            const Alias: any = C;
            new Alias(null, null);
            const bind: any = (() => 0).bind;
            const Bound: any = bind.call(C, {}, null);
            const Nested: any = Bound.bind({}, null);
            new Nested();
            new Alias(undefined, undefined);
            new C(43, true);
            """, "null null object object 2 null null\nnull null object object 2 null null\nnull null object object 2 null null\nundefined undefined undefined undefined 2 undefined undefined\n43 true number boolean 2 43 true\n"];
        yield return ["empty-typed-arguments", """
            class C {
                constructor(value: number, flag: boolean) {
                    console.log(arguments.length, arguments[0], arguments[1]);
                }
            }
            const Alias: any = C;
            new Alias();
            new Alias(1, true);
            new Alias();
            """, "0 undefined undefined\n2 1 true\n0 undefined undefined\n"];
        yield return ["snapshot-isolation", """
            class C {
                constructor(value: any, flag: any) {
                    console.log(value, flag, arguments.length, arguments[0], arguments[1]);
                }
            }
            const Alias: any = C;
            const Bound: any = C.bind({}, null);
            const Nested: any = Bound.bind({}, null);
            new Nested();
            new Alias(undefined, undefined);
            """, "null null 2 null null\nundefined undefined 2 undefined undefined\n"];
        yield return ["actual-argument-list", """
            class C {
                value: any;
                constructor(value: any, flag: any) {
                    console.log(arguments.length, arguments[0], arguments[1], arguments[2]);
                    this.value = value;
                    if (value === 'throw') throw value;
                }
            }
            const Alias: any = C;
            new Alias();
            new Alias(undefined, undefined);
            try { new Alias('throw', null, 7); } catch (error) { console.log(error); }
            console.log('nested', new Alias(43, true, 9).value);
            new Alias();
            """, "0 undefined undefined undefined\n2 undefined undefined undefined\n3 throw null 7\nthrow\n3 43 true 9\nnested 43\n0 undefined undefined undefined\n"];
        yield return ["derived", """
            class Base {
                constructor(value: number, flag: boolean) {
                    console.log("base", value, flag, typeof value, typeof flag, arguments.length);
                }
            }
            class Explicit extends Base {
                constructor(value: number, flag: boolean) {
                    super(value, flag);
                    console.log("child", value, flag, typeof value, typeof flag, arguments.length);
                }
            }
            class Implicit extends Base {}
            new Explicit(null as any, null as any);
            const Bound: any = Explicit.bind({}, null, null);
            new Bound();
            const Inherited: any = Implicit.bind({}, null, null);
            new Inherited();
            """, "base null null object object 2\nchild null null object object 2\nbase null null object object 2\nchild null null object object 2\nbase null null object object 2\n"];
        yield return ["defaults", """
            class C {
                constructor(value: number, flag: boolean = true, ...tail: any[]) {
                    console.log(value, flag, typeof value, typeof flag, tail.length, tail.length ? tail[0] : "empty");
                }
            }
            const Bound: any = C.bind({}, null, null);
            new Bound(43);
            const Alias: any = C;
            new Alias(undefined, undefined);
            new Alias();
            """, "null null object object 1 43\nundefined true undefined boolean 0 empty\nundefined true undefined boolean 0 empty\n"];
        yield return ["capture", CaptureSource,
            "raw null null object object\ncapture null null object object 43\nraw 7 false number boolean\ncapture 7 false number boolean 43\n"];
        yield return ["ordinary", OrdinaryCaptureSource, "8 number 45\n9 number 45\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[1], row[2], mode }));

    [Theory]
    [MemberData(nameof(ModeCases))]
    public void GuestConstructorArgumentsKeepSuppliedValues(string source, string expected, ExecutionMode mode)
    {
        if (mode == ExecutionMode.Compiled)
            Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
