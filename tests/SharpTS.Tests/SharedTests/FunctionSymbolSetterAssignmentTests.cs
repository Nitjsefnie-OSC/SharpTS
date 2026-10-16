using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class FunctionSymbolSetterAssignmentTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            function C() {}
            const target: any = C, key = Symbol('assignment');
            let writes = 0, value = 0, receiver = false;
            Object.defineProperty(target, key, {
                set(next: number) { writes++; value = next; receiver = this === target; }
            });
            target[key] = 11;
            console.log(value, receiver, writes);
            """, "11 true 1\n"];
        yield return ["all-forms-and-assignment-result", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol(), other = Symbol();
                const state = { writes: 0, value: 0, receiver: false };
                target[other] = 'other';
                Object.defineProperty(target, key, {
                    set(next: number) {
                        state.writes++; state.value = next; state.receiver = this === target;
                        return 99;
                    }
                });
                console.log(target[key] = 11);
                console.log(state.value, state.receiver, state.writes, target[key] === undefined, target[other]);
            }
            """, "11\n11 true 1 true other\n11\n11 true 1 true other\n11\n11 true 1 true other\n"];
        yield return ["throwing-setter", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol(), token: any = {};
                const state = { writes: 0 };
                Object.defineProperty(target, key, {
                    set(next: number) { state.writes++; throw token; }
                });
                try { target[key] = 11; } catch (error) { console.log(error === token, state.writes); }
                console.log(target[key] === undefined);
            }
            """, "true 1\ntrue\ntrue 1\ntrue\ntrue 1\ntrue\n"];
        yield return ["reflect-explicit-receiver", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                const receiver: any = { value: 0 };
                const state = { writes: 0, receiver: false };
                Object.defineProperty(target, key, {
                    set: function(this: any, next: number) {
                        state.writes++; this.value = next; state.receiver = this === receiver;
                    }
                });
                console.log(Reflect.set(target, key, 23, receiver), receiver.value, state.receiver, state.writes, target[key] === undefined);
            }
            """, "true 23 true 1 true\ntrue 23 true 1 true\ntrue 23 true 1 true\n"];
        yield return ["getter-only", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            function strictWrite(target: any, key: symbol) { 'use strict'; target[key] = 9; }
            for (const target of functions) {
                const key = Symbol();
                Object.defineProperty(target, key, { get() { return 7; } });
                console.log(target[key] = 9, target[key]);
                try { strictWrite(target, key); } catch (error) { console.log(error instanceof TypeError); }
                console.log(target[key]);
                console.log(Reflect.set(target, key, 13));
            }
            """, "9 7\ntrue\n7\nfalse\n9 7\ntrue\n7\nfalse\n9 7\ntrue\n7\nfalse\n"];
        yield return ["nonextensible-existing-setter", """
            function declaration() {}
            const functions: any[] = [declaration, function() {}, () => {}];
            for (const target of functions) {
                const key = Symbol();
                const state = { value: 0, writes: 0 };
                Object.defineProperty(target, key, {
                    set(next: number) { state.value = next; state.writes++; }
                });
                Object.preventExtensions(target);
                target[key] = 17;
                console.log(state.value, state.writes, Object.isExtensible(target));
            }
            """, "17 1 false\n17 1 false\n17 1 false\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void AssignmentInvokesTheOwnSymbolSetter(string name, string source, string expected, ExecutionMode mode)
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
