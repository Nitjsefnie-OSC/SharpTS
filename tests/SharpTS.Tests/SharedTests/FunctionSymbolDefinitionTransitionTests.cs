using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionSymbolDefinitionTransitionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-undefined-replacement", """
            function C() {}
            const target: any = C, key = Symbol('transition'), token: any = {};
            let reads = 0;
            Object.defineProperty(target, key, { get() { reads++; throw token; }, configurable: true });
            Object.defineProperty(target, key, { value: undefined, configurable: true });
            try { console.log(target[key] === undefined, reads); }
            catch (error) { console.log(error === token, reads); }
            """, "true 0\n"];
        yield return ["all-forms-and-key-isolation", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol('transition'), other = Symbol('transition');
                const state = { reads: 0 };
                target['Symbol(transition)'] = 'text';
                Object.defineProperty(target, other, { get() { return 17; }, configurable: true });
                Object.defineProperty(target, key, { get() { state.reads++; return 'old'; }, configurable: true });
                Object.defineProperty(target, key, { value: 'new', configurable: true });
                console.log(target[key], state.reads, target[other], target['Symbol(transition)']);
            }
            """, "new 0 17 text\nnew 0 17 text\nnew 0 17 text\n"];
        yield return ["writable-only-data-conversion", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                const state = { reads: 0 };
                Object.defineProperty(target, key, { get() { state.reads++; throw 1; }, configurable: true });
                Object.defineProperty(target, key, { writable: true });
                console.log(target[key] === undefined, state.reads);
            }
            """, "true 0\ntrue 0\ntrue 0\n"];
        yield return ["generic-descriptor-preserves-getter", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                const state = { reads: 0 };
                Object.defineProperty(target, key, { get() { state.reads++; return 7; }, configurable: true });
                Object.defineProperty(target, key, { enumerable: true });
                console.log(target[key], state.reads);
            }
            """, "7 1\n7 1\n7 1\n"];
        yield return ["explicit-undefined-accessor", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                const state = { reads: 0 };
                Object.defineProperty(target, key, { value: 7, configurable: true });
                Object.defineProperty(target, key, { get: undefined });
                console.log(target[key] === undefined);
                Object.defineProperty(target, key, { get() { state.reads++; throw 1; } });
                Object.defineProperty(target, key, { get: undefined });
                console.log(target[key] === undefined, state.reads);
            }
            """, "true\ntrue 0\ntrue\ntrue 0\ntrue\ntrue 0\n"];
        yield return ["merge-accessors-and-roundtrip", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                const state = { reads: 0 };
                Object.defineProperty(target, key, { value: 7, configurable: true });
                Object.defineProperty(target, key, { get() { state.reads++; return 9; } });
                Object.defineProperty(target, key, { set(value: number) {} });
                console.log(target[key], state.reads);
                Object.defineProperty(target, key, { value: 11 });
                console.log(target[key], state.reads);
            }
            """, "9 1\n11 1\n9 1\n11 1\n9 1\n11 1\n"];
        yield return ["assignment-remains-distinct", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                Object.defineProperty(target, key, { get() { return 7; }, configurable: true });
                target[key] = 9;
                console.log(target[key]);
            }
            """, "7\n7\n7\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void SymbolDefinitionsReplaceThePreviousDescriptorKind(string name, string source, string expected, ExecutionMode mode)
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
