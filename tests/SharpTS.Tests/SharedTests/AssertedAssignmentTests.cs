using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AssertedAssignmentTests
{
    [Theory, ModeData]
    public void AssertionsKeepTheWrittenWriteTypeAndEraseAtRuntime(ExecutionMode mode)
    {
        const string source = """
            let target: any = 1;
            (target as any) = 2;
            console.log(target);
            let numberValue: number = 1;
            console.log((numberValue as any) = "stored", typeof numberValue);
            (<any>numberValue) = "angle";
            console.log(numberValue);
            let dynamic: any = 1;
            console.log((dynamic as string) += 2, typeof dynamic);
            (dynamic satisfies any) = 7;
            console.log(dynamic);
            """;
        Assert.Equal("2\nstored string\nangle\n3 number\n7\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void MemberAndIndexReferencesAreEvaluatedOnceAndShortCircuit(ExecutionMode mode)
    {
        const string source = """
            let receivers = 0, keys = 0, rights = 0;
            const object: any = { value: 0 };
            const items: any = [4];
            function receiver(): any { receivers++; return object; }
            function arrayReceiver(): any { receivers++; return items; }
            function key(): number { keys++; return 0; }
            function right(): number { rights++; return 4; }
            console.log((receiver().value as any) = right());
            console.log((arrayReceiver()[key()] as number) += right());
            console.log((receiver().value as number) ||= right());
            console.log((arrayReceiver()[key()] as number) &&= right());
            console.log((receiver().value as number) ??= right());
            console.log(receivers, keys, rights, object.value);
            const nested: any = { value: { result: 0 } };
            (nested?.value as any).result = 9;
            console.log(nested.value.result);
            """;
        Assert.Equal("4\n8\n4\n4\n4\n5 2 3 4\n9\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PrivateAndSuperReferencesUseTheExistingWritePaths(ExecutionMode mode)
    {
        const string source = """
            class Base { stored: any = 0; set value(next: number) { this.stored = next; } }
            class Child extends Base {
                #private: any = 0;
                run() {
                    (this.#private as number) ||= 3;
                    (this.#private as number) += 2;
                    (super.value as number) = this.#private;
                    return this.stored;
                }
            }
            console.log(new Child().run());
            """;
        Assert.Equal("5\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void AssertionsBypassTypeScriptReadonlyButKeepJavaScriptConstErrors(ExecutionMode mode)
    {
        const string source = """
            class Readonly { readonly value: number = 1; }
            const object = new Readonly();
            (object.value as any) = 2;
            console.log(object.value);
            let calls = 0;
            function right(): number { calls++; return 7; }
            const binding: any = 1;
            try { (binding as any) = right(); } catch (error) { console.log(error instanceof TypeError); }
            try { (binding as any) += right(); } catch (error) { console.log(error instanceof TypeError); }
            console.log((binding as any) ||= right());
            console.log((binding as any) ??= right());
            try { (binding as any) &&= right(); } catch (error) { console.log(error instanceof TypeError); }
            console.log(binding, calls);
            """;
        Assert.Equal("2\ntrue\ntrue\n1\n1\ntrue\n1 3\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NonNullWritesUseTheDeclaredSlotWithoutReadingAnUninitializedVariable(ExecutionMode mode)
    {
        const string source = """
            function run() { let value: number; value! = 2; return value; }
            let nullable: number | null = null;
            nullable! = 3;
            console.log(run(), nullable);
            """;
        Assert.Equal("2 3\n", TestHarness.Run(source, mode));
    }

    public static IEnumerable<object[]> InvalidCases()
    {
        string[] sources =
        [
            "let value: any = 1; (value as number) = 'wrong';",
            "let value: any = 1; (value as number) ||= 'wrong';",
            "let value: any = 1; (value as number) += 'wrong';",
            "let value: number = 1; (value satisfies number) = 'wrong';",
            "let value: number = 1; (value as string) = 'wrong';",
            "let outer: any = 0; let inner: number = 0; (outer as any) = (inner = 'wrong');",
            "let outer: any = 0; class C { readonly value = 1; } const object = new C(); (outer as any) = (object.value = 2);",
            "let value: number | null = null; value! = null;",
            "const value: number = 1; value! = 2;",
            "class C { readonly value = 1; } const object = new C(); object.value! = 2;",
        ];
        foreach (string source in sources)
            foreach (ExecutionMode mode in new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
                yield return [source, mode];
    }

    [Theory, MemberData(nameof(InvalidCases))]
    public void WrittenTypeAndNestedWritesRemainChecked(string source, ExecutionMode mode) =>
        Assert.IsAssignableFrom<TypeCheckException>(Assert.ThrowsAny<Exception>(() => TestHarness.Run(source, mode)));
}
