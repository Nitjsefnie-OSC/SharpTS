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
    }

    // #2729: compiled constructor aliases/Reflect omit isView; #2730: compiled calls bypass local ArrayBuffer bindings.
    // Both complete controls remain interpreted; direct unshadowed classification retains both modes.
    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        ((string)row[0] is "held-and-reflect-members-share-callable-identity"
            or "parameter-and-block-shadows-use-their-own-member"
            ? new[] { ExecutionMode.Interpreted }
            : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
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
