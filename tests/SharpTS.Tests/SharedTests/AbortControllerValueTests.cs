using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AbortControllerValueTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["bare-reference", """
            const ctor: any = AbortController;
            console.log(typeof ctor);
            """, "function\n"];
        yield return ["bare-typeof", """
            console.log(typeof AbortController);
            """, "function\n"];
        yield return ["canonical-identity", """
            const ctor: any = AbortController;
            console.log(ctor === AbortController);
            function identity(value: any) { return value; }
            console.log(identity(AbortController) === ctor, typeof identity(AbortController));
            """, "true\ntrue function\n"];
        yield return ["constructor-metadata", """
            const ctor: any = AbortController;
            console.log(ctor.name, ctor.length);
            console.log(Reflect.get(ctor, 'name'), Reflect.get(ctor, 'length'));
            """, "AbortController 0\nAbortController 0\n"];
        yield return ["global-object-identity", """
            const ctor: any = Reflect.get(globalThis, 'AbortController');
            console.log(ctor === AbortController, globalThis.AbortController === ctor);
            console.log(new ctor().signal.aborted);
            """, "true true\nfalse\n"];
        yield return ["aliased-construction", """
            const ctor: any = AbortController;
            const controller: any = new ctor();
            console.log(controller.signal.aborted);
            controller.abort('reason');
            console.log(controller.signal.aborted, controller.signal.reason);
            const other: any = new ctor();
            console.log(other.signal.aborted, other.signal !== controller.signal);
            """, "false\ntrue reason\nfalse true\n"];
        yield return ["prior-direct-construction", """
            const direct = new AbortController();
            const ctor: any = AbortController;
            console.log(typeof ctor, direct.signal.aborted);
            console.log(new ctor().signal.aborted);
            """, "function false\nfalse\n"];
        yield return ["direct-and-alias-membership", """
            const direct: any = new AbortController();
            const ctor: any = AbortController;
            const alias: any = new ctor();
            console.log(direct instanceof ctor, alias instanceof ctor);
            """, "true true\n"];
        yield return ["reflection-and-container", """
            const box: any = { ctor: AbortController };
            const ctor: any = Reflect.get(box, 'ctor');
            console.log(ctor === AbortController);
            const controller: any = Reflect.construct(ctor, []);
            controller.abort('reflected');
            console.log(controller.signal.aborted, controller.signal.reason);
            """, "true\ntrue reflected\n"];
        yield return ["async-and-generator-values", """
            async function read() {
                await Promise.resolve(1);
                const ctor: any = AbortController;
                console.log(typeof ctor, ctor === AbortController, new ctor().signal.aborted);
            }
            function* values() { yield 1; yield AbortController; }
            const iterator = values();
            iterator.next();
            const ctor: any = iterator.next().value;
            console.log(ctor === AbortController, typeof ctor);
            read();
            """, "true function\nfunction true false\n"];
        yield return ["lexical-shadow", """
            const original: any = AbortController;
            function local(AbortController: any) { return AbortController; }
            console.log(local(17), original === AbortController);
            {
                const AbortController: any = 'local';
                console.log(AbortController);
            }
            console.log(typeof AbortController);
            """, "17 true\nlocal\nfunction\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // Generic interpreted constructor names/global object identities are tracked in #2363/#2364.
            .Where(mode => mode == ExecutionMode.Compiled || (string)row[0] is not
                ("constructor-metadata" or "global-object-identity"))
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ConstructorIsAvailableAsAStableValue(string name, string source, string expected, ExecutionMode mode)
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
