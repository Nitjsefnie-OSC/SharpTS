using System.Reflection;
using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.Compilation;

/// <summary>
/// Regression tests for #721: an <c>async</c> arrow nested in an <c>async</c> class method got TWO async
/// state machines emitted — the live nested one (which the method invokes) and a redundant standalone one
/// (<c>&lt;&gt;c__AsyncArrow_N</c> with <c>&lt;&gt;captured_*</c> fields) that is never invoked.
/// <c>DefineTopLevelAsyncArrows</c> ran before class-method state machines were registered, so a method's
/// arrows looked "unclaimed" and got a dead standalone builder. The fix skips an arrow that an enclosing
/// async function/method's state machine will claim (walking the enclosing-callable chain), while leaving
/// arrows behind a sync arrow/method — or genuinely top-level — with their needed standalone builder.
/// </summary>
public class AsyncArrowNoDeadDuplicateTests
{
    private static int CountAsyncArrowTypes(Assembly asm)
    {
        Type?[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }
        return types.Count(t => t?.Name?.Contains("AsyncArrow") == true);
    }

    private static void AssertNestedArrowRepresentations(Assembly assembly, int arrowsPerBody)
    {
        Type[] arrows = assembly.GetTypes().Where(type => type.Name.Contains("AsyncArrow")).ToArray();
        // Typed calls and canonical guest method values have separate, reachable
        // bodies. Neither may also emit the old dead standalone arrow from #721.
        Assert.Equal(arrowsPerBody * 2, arrows.Length);
        Assert.All(arrows, arrow => Assert.NotNull(arrow.GetField("<>__outer",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)));
    }

    [Fact]
    public void AsyncArrowInAsyncMethod_EmitsOnlyItsLiveReceiverRepresentations()
    {
        // The #721 repro: one async arrow `w` directly inside the async method `run`.
        var source = """
            class C {
              value = 5;
              async run() {
                const w = async () => { await Promise.resolve(1); return this.value; };
                return await w();
              }
            }
            new C().run().then(r => console.log(r));
            const borrowed: any = new C().run;
            borrowed.call({ value: 9 }).then((r: any) => console.log(r));
            """;

        Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        var (assembly, output) = TestHarness.CompileAndRun(source, DecoratorMode.None);

        Assert.Equal("5\n9\n", output);
        AssertNestedArrowRepresentations(assembly, 1);
    }

    [Fact]
    public void NestedAsyncArrowsInAsyncMethod_NoDeadDuplicates()
    {
        // Both arrows are claimed by their containing method representation;
        // neither may acquire an additional standalone state machine.
        var source = """
            class C {
              value = 8;
              async run() {
                const a = async () => {
                  const w = async () => { await Promise.resolve(1); return this.value; };
                  return await w();
                };
                return await a();
              }
            }
            new C().run().then(r => console.log(r));
            const borrowed: any = new C().run;
            borrowed.call({ value: 11 }).then((r: any) => console.log(r));
            """;

        Assert.Empty(TestHarness.CompileAndVerifyOnly(source));
        var (assembly, output) = TestHarness.CompileAndRun(source, DecoratorMode.None);

        Assert.Equal("8\n11\n", output);
        AssertNestedArrowRepresentations(assembly, 2);
    }

    [Fact]
    public void AsyncArrowInSyncMethod_StillEmitsItsStandalone()
    {
        // An async arrow behind a SYNC method is not claimed by any async state machine, so it still
        // needs (and gets) its standalone builder — the skip must not over-reach.
        var source = """
            class C {
              m() {
                const w = async () => { await Promise.resolve(1); return 5; };
                return w;
              }
            }
            const f = new C().m();
            f().then(r => console.log(r));
            """;

        var (assembly, output) = TestHarness.CompileAndRun(source, DecoratorMode.None);

        Assert.Equal("5\n", output);
        Assert.Equal(1, CountAsyncArrowTypes(assembly));
    }

    [Fact]
    public void AsyncArrowBehindSyncArrowInAsyncMethod_StillEmitsItsStandalone()
    {
        // An async arrow behind a SYNC arrow (itself in an async method) is not claimed either — the
        // sync arrow breaks the async chain — so its standalone builder must remain.
        var source = """
            class C {
              async run() {
                const a = () => {
                  const w = async () => { await Promise.resolve(1); return 7; };
                  return w;
                };
                return await a()();
              }
            }
            new C().run().then(r => console.log(r));
            """;

        var (assembly, output) = TestHarness.CompileAndRun(source, DecoratorMode.None);

        Assert.Equal("7\n", output);
        Assert.Equal(1, CountAsyncArrowTypes(assembly));
    }
}
