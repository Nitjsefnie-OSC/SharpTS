using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArrayBufferStaticMemberTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["direct-isview-classifies-buffer-and-nonview-values", """
            const buffer = new ArrayBuffer(8);
            const view = new DataView(buffer);
            const typed = new Uint8Array(buffer);
            console.log(ArrayBuffer.isView(view), ArrayBuffer.isView(typed), ArrayBuffer.isView(buffer));
            console.log(ArrayBuffer.isView({}), ArrayBuffer.isView(null), ArrayBuffer.isView([1, 2]), ArrayBuffer.isView(7));
            console.log(buffer.byteLength, view.byteLength);
            """, "true true false\nfalse false false false\n8 8\n"];
        yield return ["held-and-reflect-members-share-callable-identity", """
            const owner: any = ArrayBuffer;
            const held: any = owner.isView, reflected: any = Reflect.get(owner, "isView");
            console.log(held === owner.isView, held === reflected, held === ArrayBuffer.isView);
            console.log(held(new DataView(new ArrayBuffer(4))), reflected(new Uint8Array(1)), held({}));
            """, "true true true\ntrue true false\n"];
        yield return ["parameter-and-block-shadows-use-their-own-member", """
            function inspect(ArrayBuffer: any) { return ArrayBuffer.isView(7); }
            console.log(inspect({ isView(value: any) { return "parameter:" + value; } }));
            {
                const ArrayBuffer: any = { isView(value: any) { return "lexical:" + value; } };
                console.log(ArrayBuffer.isView(9));
            }
            console.log(ArrayBuffer.isView(new Uint8Array(1)));
            """, "parameter:7\nlexical:9\ntrue\n"];
        yield return ["direct-classification-and-native-allocation-preservation", """
            const buffer = new ArrayBuffer(8);
            const view = new DataView(buffer, 2, 4);
            const bytes = new Uint8Array(buffer);
            const words = new Int16Array(buffer);
            view.setUint8(0, 37);
            console.log(ArrayBuffer.isView(view), ArrayBuffer.isView(bytes), ArrayBuffer.isView(words));
            console.log(ArrayBuffer.isView(buffer), ArrayBuffer.isView({}), ArrayBuffer.isView(null), ArrayBuffer.isView([1]));
            console.log(buffer.byteLength, view.byteLength, bytes.length, words.length, bytes[2]);
            const shared = new SharedArrayBuffer(8);
            const sharedView = new DataView(shared);
            const sharedWords = new Int32Array(shared);
            console.log(ArrayBuffer.isView(sharedView), ArrayBuffer.isView(sharedWords), ArrayBuffer.isView(shared));
            console.log(shared.byteLength, sharedView.byteLength, sharedWords.length);
            """, "true true true\nfalse false false false\n8 4 8 4 37\ntrue true false\n8 8 2\n"];
        yield return ["exact-arraybuffer-alias-isview-members", """
            const constructor: any = ArrayBuffer;
            console.log(typeof ArrayBuffer.isView, typeof constructor.isView, typeof Reflect.get(constructor, "isView"));
            const buffer = new ArrayBuffer(8);
            const view = new DataView(buffer);
            console.log(buffer.byteLength, view.byteLength);
            """, "function function function\n8 8\n"];
        yield return ["held-call-apply-bind-and-parameter-owner-identity", """
            const owner: any = ArrayBuffer;
            const held: any = owner.isView;
            const view = new DataView(new ArrayBuffer(4));
            const typed = new Uint8Array(2);
            console.log(held.call(null, view), held.apply({ marker: 1 }, [typed]));
            const bound: any = held.bind({ marker: 2 });
            console.log(bound(view), bound({}), held.call(undefined, new ArrayBuffer(1)));
            function inspect(constructor: any, value: any) {
                const member: any = constructor.isView;
                const reflected: any = Reflect.get(constructor, 'isView');
                console.log(member === held, reflected === member, member(value), reflected({}));
            }
            inspect(owner, typed);
            """, "true true\ntrue false false\ntrue true true false\n"];
        yield return ["repeated-member-cache-and-own-descriptor-identity", """
            const owner: any = ArrayBuffer;
            const first: any = owner.isView;
            const second: any = owner.isView;
            const reflected: any = Reflect.get(owner, 'isView');
            console.log(typeof first, first === second, first === reflected, first === ArrayBuffer.isView);
            const descriptor = Object.getOwnPropertyDescriptor(owner, 'isView')!;
            console.log(Object.hasOwn(owner, 'isView'), descriptor.value === first);
            console.log(descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(descriptor.get === undefined, descriptor.set === undefined);
            console.log(first(new Uint8Array(1)), second(new DataView(new ArrayBuffer(2))), reflected({}));
            """, "function true true true\ntrue true\ntrue false true\ntrue true\ntrue true false\n"];
    }

    // #2730 owns compiled calls bypassing local ArrayBuffer bindings.
    public static IEnumerable<object[]> CompiledCases() => Cases().Where(row =>
        (string)row[0] != "parameter-and-block-shadows-use-their-own-member");

    // #2822 owns interpreted Object.hasOwn missing the present isView member;
    // preserve the full descriptor control in compiled and isolated saved execution.
    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        ((string)row[0] switch
        {
            "parameter-and-block-shadows-use-their-own-member" => new[] { ExecutionMode.Interpreted },
            "repeated-member-cache-and-own-descriptor-identity" => new[] { ExecutionMode.Compiled },
            _ => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
        })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void StaticMembersRespectTheRuntimeConstructorAndLexicalBinding(
        string name, string source, string expected, ExecutionMode mode)
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
