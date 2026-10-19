using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PromiseExpandoDescriptorTests
{
    [Theory, ModeData]
    public void DataDescriptorAttributesControlWritesAndDeletion(ExecutionMode mode)
    {
        const string source = """
            function write(target: any) { 'use strict'; target.locked = 2; }
            function remove(target: any) { 'use strict'; return delete target.locked; }
            const promise: any = Promise.resolve(0);
            Object.defineProperty(promise, 'open', {
                value: 3, writable: true, enumerable: true, configurable: true
            });
            const open: any = Object.getOwnPropertyDescriptor(promise, 'open');
            console.log(open.value, open.writable, open.enumerable, open.configurable);
            Object.defineProperty(promise, 'locked', {
                value: 1, writable: false, enumerable: false, configurable: false
            });
            const locked: any = Object.getOwnPropertyDescriptor(promise, 'locked');
            console.log(locked.value, locked.writable, locked.enumerable, locked.configurable);
            promise.locked = 2;
            console.log(promise.locked, delete promise.locked, Object.hasOwn(promise, 'locked'));
            try { write(promise); } catch (error) { console.log(error instanceof TypeError); }
            try { remove(promise); } catch (error) { console.log(error instanceof TypeError); }
            try { Object.defineProperty(promise, 'locked', { value: 2 }); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(delete promise.open, Object.hasOwn(promise, 'open'),
                Object.getOwnPropertyDescriptor(promise, 'open') === undefined);
            """;
        string output;
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, compiledOutput) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            output = compiledOutput;
        }
        else
        {
            output = TestHarness.Run(source, mode);
        }
        Assert.Equal("3 true true true\n1 false false false\n1 false true\ntrue\ntrue\ntrue\ntrue false true\n", output);
    }

    [Theory, ModeData]
    public void AccessorReplacementAndPartialDefinitionsPreserveAttributes(ExecutionMode mode)
    {
        const string source = """
            const promise: any = Promise.resolve(0);
            Object.defineProperty(promise, 'slot', {
                get: function() { console.log(this === promise); return 5; },
                set: function(value: any) { console.log(this === promise, value); },
                enumerable: true, configurable: true
            });
            promise.slot = 7;
            console.log(promise.slot);
            Object.defineProperty(promise, 'slot', { value: 9, writable: true });
            promise.slot = 10;
            const data: any = Object.getOwnPropertyDescriptor(promise, 'slot');
            console.log(data.value, data.writable, data.enumerable, data.configurable, Object.hasOwn(data, 'get'));
            Object.defineProperty(promise, 'slot', { enumerable: false });
            const updated: any = Object.getOwnPropertyDescriptor(promise, 'slot');
            console.log(promise.slot, updated.writable, updated.enumerable, updated.configurable);
            console.log(Reflect.deleteProperty(promise, 'slot'), Object.hasOwn(promise, 'slot'));
            """;
        string output;
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, compiledOutput) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            output = compiledOutput;
        }
        else
        {
            output = TestHarness.Run(source, mode);
        }
        Assert.Equal("true 7\ntrue\n5\n10 true true true false\n10 true false true\ntrue false\n", output);
    }

    [Theory, ModeData]
    public void SubclassFieldsAndIntegrityOperationsUseTheSameDescriptors(ExecutionMode mode)
    {
        const string source = """
            class P extends Promise<any> {
                field = 1;
                constructor() { super((resolve: any) => resolve(0)); }
            }
            const promise: any = new P();
            Object.defineProperty(promise, 'field', { enumerable: false });
            promise.field = 2;
            const before: any = Object.getOwnPropertyDescriptor(promise, 'field');
            console.log(before.value, before.writable, before.enumerable, before.configurable);
            Object.seal(promise);
            promise.field = 3;
            const sealed: any = Object.getOwnPropertyDescriptor(promise, 'field');
            console.log(sealed.value, sealed.writable, sealed.enumerable, sealed.configurable, delete promise.field);
            Object.freeze(promise);
            promise.field = 4;
            promise.missing = 5;
            const frozen: any = Object.getOwnPropertyDescriptor(promise, 'field');
            console.log(frozen.value, frozen.writable, frozen.enumerable, frozen.configurable,
                Object.hasOwn(promise, 'missing'));
            """;
        string output;
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, compiledOutput) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            output = compiledOutput;
        }
        else
        {
            output = TestHarness.Run(source, mode);
        }
        Assert.Equal("2 true false true\n3 true false false false\n3 false false false false\n", output);
    }

    [Theory, ModeData]
    public void AbsentStringDeletionSucceedsAfterIntegrityOperations(ExecutionMode mode)
    {
        const string source = """
            function strictRemove(target: any, key: string): boolean {
                "use strict";
                return delete target[key];
            }
            for (const kind of ["seal", "freeze"]) {
                const promise: any = Promise.resolve(0);
                promise.extra = 1;
                Object.defineProperty(promise, "locked", {
                    value: 3, writable: false, enumerable: false, configurable: false
                });
                if (kind === "seal") Object.seal(promise);
                else Object.freeze(promise);
                console.log(kind, Reflect.deleteProperty(promise, "missing"), delete promise.missing,
                    strictRemove(promise, "missing"));
                console.log(Reflect.deleteProperty(promise, "then"), delete promise.then,
                    strictRemove(promise, "then"), typeof promise.then, Object.hasOwn(promise, "then"));
                console.log(Reflect.deleteProperty(promise, "locked"), delete promise.locked,
                    Object.hasOwn(promise, "locked"), promise.locked);
                try { strictRemove(promise, "locked"); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            """;
        const string expected = "seal true true true\ntrue true true function false\nfalse false true 3\ntrue\nfreeze true true true\ntrue true true function false\nfalse false true 3\ntrue\n";
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
