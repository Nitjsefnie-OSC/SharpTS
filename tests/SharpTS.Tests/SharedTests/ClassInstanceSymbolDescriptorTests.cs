using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassInstanceSymbolDescriptorTests
{
    [Theory, ModeData]
    public void ExplicitAttributesControlSymbolWritesAndDeletion(ExecutionMode mode)
    {
        const string source = """
            function write(target: any, key: symbol) { 'use strict'; target[key] = 3; }
            function remove(target: any, key: symbol) { 'use strict'; return delete target[key]; }
            const key = Symbol('slot');
            class C {}
            const instance: any = new C();
            Object.defineProperty(instance, key, {
                value: 1, writable: false, enumerable: true, configurable: false
            });
            const descriptor: any = Object.getOwnPropertyDescriptor(instance, key);
            console.log(descriptor.value, descriptor.writable, descriptor.enumerable, descriptor.configurable);
            instance[key] = 2;
            console.log(instance[key], delete instance[key], instance[key]);
            try { write(instance, key); } catch (error) { console.log(error instanceof TypeError); }
            try { remove(instance, key); } catch (error) { console.log(error instanceof TypeError); }
            try { Object.defineProperty(instance, key, { value: 4 }); }
            catch (error) { console.log(error instanceof TypeError); }
            console.log(instance[key], Object.hasOwn(instance, key));
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
        Assert.Equal("1 false true false\n1 false 1\ntrue\ntrue\ntrue\n1 true\n", output);
    }

    [Theory, ModeData]
    public void ClassFieldsAndIntegrityOperationsShareSymbolDescriptors(ExecutionMode mode)
    {
        const string source = """
            const key = Symbol('field'), missing = Symbol('missing');
            class C { [key]: number = 1; }
            for (const kind of ['freeze', 'seal', 'prevent']) {
                const instance: any = new C();
                const before: any = Object.getOwnPropertyDescriptor(instance, key);
                console.log(before.value, before.writable, before.enumerable, before.configurable);
                if (kind === 'freeze') Object.freeze(instance);
                else if (kind === 'seal') Object.seal(instance);
                else Object.preventExtensions(instance);
                instance[key] = 2;
                instance[missing] = 3;
                const after: any = Object.getOwnPropertyDescriptor(instance, key);
                console.log(after.value, after.writable, after.enumerable, after.configurable,
                    Object.hasOwn(instance, missing), delete instance[key]);
            }
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
        Assert.Equal("1 true true true\n1 false true false false false\n" +
            "1 true true true\n2 true true false false false\n" +
            "1 true true true\n2 true true true false true\n", output);
    }

    [Theory, ModeData]
    public void SymbolAccessorReplacementPreservesSpecifiedAttributes(ExecutionMode mode)
    {
        const string source = """
            const key = Symbol('accessor');
            class C {}
            const instance: any = new C();
            let stored = 1;
            Object.defineProperty(instance, key, {
                get: function() { console.log(this === instance); return stored; },
                set: function(value: number) { console.log(this === instance); stored = value; },
                enumerable: true, configurable: true
            });
            instance[key] = 7;
            console.log(instance[key]);
            Object.defineProperty(instance, key, { value: 8, writable: false });
            instance[key] = 9;
            const descriptor: any = Object.getOwnPropertyDescriptor(instance, key);
            console.log(instance[key], descriptor.writable, descriptor.enumerable, descriptor.configurable,
                Object.hasOwn(descriptor, 'get'));
            console.log(delete instance[key], instance[key] === undefined);
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
        Assert.Equal("true\ntrue\n7\n8 false true true false\ntrue true\n", output);
    }
}
