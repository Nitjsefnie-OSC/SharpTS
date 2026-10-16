using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AwaitOperandExceptionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            function fail() { throw new TypeError('boom'); }
            async function check() {
                try { await fail(); return 'missed'; }
                catch (error: any) { return error instanceof TypeError ? 'caught' : 'wrong'; }
            }
            check().then(value => console.log(value))
                .catch(error => console.log('TOP', error.name, error.message));
            """, "caught\n"];
        yield return ["statement-positions", """
            function fail(): any { throw new TypeError('boom'); }
            async function variable() { try { const value = await fail(); return value; } catch { return 'variable'; } }
            async function returned() { try { return await fail(); } catch { return 'return'; } }
            async function argument() { try { console.log('missed', await fail()); } catch { return 'argument'; } }
            async function run() {
                console.log(await variable(), await returned(), await argument());
            }
            run();
            """, "variable return argument\n"];
        yield return ["getter-and-call-argument", """
            const token: any = { marker: 'same' };
            const holder: any = {};
            Object.defineProperty(holder, 'value', { get() { throw token; } });
            function fail(): any { throw token; }
            async function run() {
                try { await holder.value; console.log('missed'); }
                catch (error) { console.log(error === token); }
                try { await Promise.resolve(fail()); console.log('missed'); }
                catch (error) { console.log(error === token); }
            }
            run();
            """, "true\ntrue\n"];
        yield return ["after-real-suspension", """
            const state: any = { resolve: null };
            function later() { return new Promise<number>(resolve => { state.resolve = resolve; }); }
            function fail(): any { throw new TypeError('boom'); }
            async function run() {
                try { console.log('before'); await later(); await fail(); console.log('missed'); }
                catch (error) { console.log(error instanceof TypeError); }
                finally { console.log('finally'); }
            }
            const pending = run();
            console.log('pending');
            state.resolve(0);
            pending.then(() => console.log('done'));
            """, "before\npending\ntrue\nfinally\ndone\n"];
        yield return ["nested-await-operand", """
            const token: any = { marker: 'same' }, state: any = { resolve: null };
            function later() { return new Promise<number>(resolve => { state.resolve = resolve; }); }
            function fail(value: number): any { console.log(value); throw token; }
            async function run() {
                try { await fail(await later()); console.log('missed'); }
                catch (error) { console.log(error === token); }
                finally { console.log('finally'); }
            }
            const pending = run();
            console.log('pending');
            state.resolve(7);
            pending.then(() => console.log('done'));
            """, "pending\n7\ntrue\nfinally\ndone\n"];
        yield return ["await-in-catch-and-finally", """
            function fail(): any { throw new TypeError('boom'); }
            async function check() {
                try { await fail(); return 'missed'; }
                catch (error: any) { await Promise.resolve(0); return 'caught:' + error.message; }
                finally { await Promise.resolve(0); console.log('finally'); }
            }
            check().then(value => console.log(value));
            """, "finally\ncaught:boom\n"];
        yield return ["nested-catch-rethrow", """
            const token: any = {};
            function fail(): any { throw token; }
            async function check() {
                try {
                    try { await fail(); console.log('missed'); }
                    catch (error) { console.log('inner', error === token); throw error; }
                    finally { console.log('inner-finally'); }
                }
                catch (error) { console.log('outer', error === token); }
                finally { console.log('outer-finally'); }
            }
            check();
            """, "inner true\ninner-finally\nouter true\nouter-finally\n"];
        yield return ["loop-abrupt-completion", """
            function fail(): any { throw new TypeError('boom'); }
            async function check() {
                for (let i = 0; i < 3; i++) {
                    try {
                        if (i === 0) await fail();
                        await Promise.resolve(0);
                        if (i === 1) continue;
                        break;
                    }
                    catch { console.log('caught', i); }
                    finally { console.log('finally', i); }
                }
                console.log('done');
            }
            check();
            """, "caught 0\nfinally 0\nfinally 1\nfinally 2\ndone\n"];
        yield return ["catch-operand-reaches-outer", """
            const first: any = {}, second: any = {};
            function fail(): any { throw second; }
            async function check() {
                try {
                    try { await Promise.reject(first); }
                    catch (error) { console.log(error === first); await fail(); console.log('missed'); }
                }
                catch (error) { console.log(error === second); }
                finally { console.log('finally'); }
            }
            check();
            """, "true\ntrue\nfinally\n"];
        yield return ["awaited-finally-before-outer-catch", """
            const token: any = {}, state: any = { resolve: null };
            function later() { return new Promise<number>(resolve => { state.resolve = resolve; }); }
            function fail(): any { throw token; }
            async function check() {
                try {
                    try { await fail(); console.log('missed'); }
                    finally { console.log('pending-finally'); await later(); console.log('inner-finally'); }
                }
                catch (error) { console.log(error === token); }
                finally { console.log('outer-finally'); }
            }
            const pending = check();
            console.log('pending');
            state.resolve(0);
            pending.then(() => console.log('done'));
            """, "pending-finally\npending\ninner-finally\ntrue\nouter-finally\ndone\n"];
        yield return ["early-return-and-finally", """
            function fail(): any { throw new TypeError('boom'); }
            async function check(early: boolean) {
                try { if (early) return 'early'; await fail(); return 'missed'; }
                catch { return 'caught'; }
                finally { console.log('finally'); }
            }
            async function run() { console.log(await check(true)); console.log(await check(false)); }
            run();
            """, "finally\nearly\nfinally\ncaught\n"];
        yield return ["success-and-rejection-controls", """
            async function check() {
                try { console.log(await Promise.resolve('ok')); }
                finally { console.log('success-finally'); }
                try { await Promise.reject(new TypeError('rejected')); }
                catch (error: any) { console.log(error.message); }
                finally { console.log('rejection-finally'); }
            }
            check();
            """, "ok\nsuccess-finally\nrejected\nrejection-finally\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OperandEvaluationStaysInsideTheGuestExceptionHandler(
        string name, string source, string expected, ExecutionMode mode)
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
