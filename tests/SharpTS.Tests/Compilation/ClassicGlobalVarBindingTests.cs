using SharpTS.Execution;
using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.Compilation;

[Collection("CompilationService")]
public sealed class ClassicGlobalVarBindingTests
{
    public static IEnumerable<object[]> ClassicCases()
    {
        yield return ["issue", """
            var directValue = 7;
            directValue = 9;
            console.log((globalThis as any).directValue, directValue);
            """, "9 9\n"];
        yield return ["block-and-handler-hoisted-initializers", """
            { var blockValue = 7; }
            try { var handlerTryValue = 8; }
            finally { var handlerFinallyValue = 9; }
            console.log((globalThis as any).blockValue, blockValue);
            console.log((globalThis as any).handlerTryValue, (globalThis as any).handlerFinallyValue, handlerTryValue, handlerFinallyValue);
            """, "7 7\n8 9 8 9\n"];
        yield return ["compound-update-and-logical-writes", """
            var changed = 1;
            changed += 2;
            console.log((globalThis as any).changed, changed);
            console.log(changed++, (globalThis as any).changed, changed);
            console.log(++changed, (globalThis as any).changed);
            var logical: any = 0;
            logical ||= 7;
            console.log((globalThis as any).logical, logical);
            logical &&= 9;
            console.log((globalThis as any).logical, logical);
            var nullable: any = null;
            nullable ??= 11;
            console.log((globalThis as any).nullable, nullable);
            """, "3 3\n3 4 4\n5 5\n7 7\n9 9\n11 11\n"];
        yield return ["destructuring-writes-and-var-patterns", """
            var first = 0, second = 0;
            [first, second] = [7, 8];
            console.log((globalThis as any).first, (globalThis as any).second, first, second);
            ({ a: first, b: second } = { a: 9, b: 10 });
            console.log((globalThis as any).first, (globalThis as any).second, first, second);
            var [left, right] = [11, 12];
            var { value: objectValue } = { value: 13 };
            console.log((globalThis as any).left, (globalThis as any).right, (globalThis as any).objectValue);
            """, "7 8 7 8\n9 10 9 10\n11 12 13\n"];
        yield return ["loop-var-writes", """
            for (var count = 0; count < 2; count++) {}
            var selected: any;
            for (selected of [3, 4]) {}
            var property: any;
            for (property in { last: 1 }) {}
            console.log((globalThis as any).count, count, (globalThis as any).selected, selected, (globalThis as any).property, property);
            """, "2 2 4 4 last last\n"];
        yield return ["lexical-and-local-shadow-isolation", """
            var scopeValue = 1;
            function local() {
                var scopeValue = 7;
                scopeValue = 9;
                console.log((globalThis as any).scopeValue, scopeValue);
            }
            local();
            {
                let scopeValue = 31;
                scopeValue = 32;
                console.log((globalThis as any).scopeValue, scopeValue);
            }
            let lexicalValue = 1;
            lexicalValue = 2;
            const constantValue = 3;
            console.log(Object.hasOwn(globalThis, "lexicalValue"), Object.hasOwn(globalThis, "constantValue"), lexicalValue, constantValue);
            scopeValue = 2;
            console.log((globalThis as any).scopeValue, scopeValue);
            """, "1 9\n1 32\nfalse false 2 3\n2 2\n"];
        yield return ["initial-publication-positive", """
            var initialValue = 7;
            var uninitialized: any;
            console.log((globalThis as any).initialValue, initialValue, Object.hasOwn(globalThis, "initialValue"));
            console.log((globalThis as any).uninitialized === undefined, uninitialized === undefined, Object.hasOwn(globalThis, "uninitialized"));
            """, "7 7 true\ntrue true true\n"];
        yield return ["global-property-writes-and-readonly-barrier", """
            var sharedValue = 1;
            (globalThis as any).sharedValue = 7;
            console.log((globalThis as any).sharedValue, sharedValue);
            Object.defineProperty(globalThis, "sharedValue", { value: 9 });
            console.log((globalThis as any).sharedValue, sharedValue);
            console.log(Reflect.set(globalThis, "sharedValue", 11), (globalThis as any).sharedValue, sharedValue);
            var lockedValue = 1;
            Object.defineProperty(globalThis, "lockedValue", { value: 1, writable: false });
            lockedValue = 2;
            console.log((globalThis as any).lockedValue, lockedValue);
            function strictWrite() { "use strict"; lockedValue = 3; }
            try { strictWrite(); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log((globalThis as any).lockedValue, lockedValue);
            """, "7 7\n9 9\ntrue 11 11\n1 1\ntrue\n1 1\n"];
        yield return ["strict-classic-publication-and-guards", """
            "use strict";
            var strictValue = 7;
            console.log((globalThis as any).strictValue, strictValue);
            strictValue = 9;
            var strictValue: number;
            const descriptor = Object.getOwnPropertyDescriptor(globalThis, "strictValue")!;
            console.log((globalThis as any).strictValue, strictValue, descriptor.writable, descriptor.enumerable, descriptor.value);
            """, "7 7\n9 9 true true 9\n"];
        yield return ["async-closure-global-write-after-await", """
            var asyncValue = 1;
            async function run() {
                const marker = { tag: "local" };
                await Promise.resolve(0);
                asyncValue = 9;
                console.log((globalThis as any).asyncValue, asyncValue, marker.tag);
            }
            run();
            """, "9 9 local\n"];
    }

    [Theory, MemberData(nameof(ClassicCases))]
    public void SourceServiceClassicVarsShareTheLiveGlobalProperty(string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        var result = SourceExecutionService.Interpret(source);
        Assert.True(result.Success, string.Join("\n", result.Errors) + result.Output);
        Assert.Empty(result.Errors);
        Assert.Equal(expected, result.Output.Replace("\r\n", "\n"));
    }

    [Fact]
    public void RepeatedInterpretationPreservesAnExistingUnmappedValue()
    {
        using var output = new StringWriter();
        using var interpreter = new Interpreter(output, TextWriter.Null);
        InterpretInRealm(interpreter, "function saved() { return 7; }");
        InterpretInRealm(interpreter, "var saved; console.log(typeof saved, saved());");
        InterpretInRealm(interpreter, "(globalThis as any).saved = () => 9;");
        InterpretInRealm(interpreter, "var saved; console.log(saved());");
        Assert.Equal("function 7\n9\n", output.ToString().Replace("\r\n", "\n"));
    }

    [Fact]
    public void ExistingAccessorIsNotInvokedByRegistrationOrDispatchGuards()
    {
        using var output = new StringWriter();
        using var interpreter = new Interpreter(output, TextWriter.Null);
        InterpretInRealm(interpreter, """
            let current: any = () => 17, reads = 0, writes = 0;
            Object.defineProperty(globalThis, "callable", {
                configurable: true,
                get() { reads++; return current; },
                set(value: any) { writes++; current = value; }
            });
            """);
        InterpretInRealm(interpreter, """
            var callable;
            console.log(reads, writes);
            console.log(callable(), reads, writes);
            callable = () => 21;
            console.log(callable(), reads, writes);
            """);
        Assert.Equal("0 0\n17 1 0\n21 2 1\n", output.ToString().Replace("\r\n", "\n"));
    }

    // Each source runs in the same embedding realm; later sources may refer to
    // declarations from earlier sources, so resolve their runtime AST directly.
    private static void InterpretInRealm(Interpreter interpreter, string source)
    {
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        new VariableResolver(interpreter).Resolve(statements);
        interpreter.Interpret(statements);
        Assert.Null(interpreter.LastUncaughtError);
    }

    [Fact]
    public void ModuleVarsRemainPrivateToTheirModule()
    {
        var files = new Dictionary<string, string>
        {
            ["main.ts"] = """
                var moduleValue = 1;
                moduleValue = 9;
                var { value: modulePattern } = { value: 11 };
                console.log(Object.hasOwn(globalThis, "moduleValue"), Object.hasOwn(globalThis, "modulePattern"), moduleValue, modulePattern);
                export {};
                """
        };
        Assert.Equal("false false 9 11\n", TestHarness.RunModules(files, "main.ts", ExecutionMode.Interpreted));
    }
}
