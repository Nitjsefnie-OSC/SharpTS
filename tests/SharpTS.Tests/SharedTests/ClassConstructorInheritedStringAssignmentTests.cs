using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassConstructorInheritedStringAssignmentTests
{
    [Theory, ModeData]
    public void FunctionPrototypeDescriptorsControlDotAndIndexedAssignments(ExecutionMode mode)
    {
        const string source = """
            function dot(target: any, value: any) { 'use strict'; target.__constructorSetControl = value; }
            function indexed(target: any, value: any) { 'use strict'; target['__constructorSetControl'] = value; }
            const functions: any = Function.prototype;
            const key = '__constructorSetControl';
            class C {}
            const owner: any = C;
            for (const write of [dot, indexed]) {
                Object.defineProperty(functions, key, { value: 1, writable: false, configurable: true });
                owner[key] = 9;
                try { write(owner, 2); } catch (error) { console.log(error instanceof TypeError); }
                console.log(Object.hasOwn(owner, key), owner[key], functions[key]);
                delete functions[key];
                let calls = 0, correct = false;
                Object.defineProperty(functions, key, {
                    set: function(value: any) { calls++; correct = this === C && value === 3; }, configurable: true
                });
                write(owner, 3);
                console.log(calls, correct, Object.hasOwn(owner, key));
                delete functions[key];
            }
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true\nfalse 1 1\n1 true false\n", 2)),
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void NearestDescriptorMasksFartherPrototypeProperties(ExecutionMode mode)
    {
        const string source = """
            function write(target: any, value: any) { 'use strict'; target.__constructorTailControl = value; }
            const functions: any = Function.prototype, objects: any = Object.prototype;
            const key = '__constructorTailControl';
            class C {}
            Object.defineProperty(objects, key, { value: 1, writable: false, configurable: true });
            try { write(C, 2); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.hasOwn(C, key), (C as any)[key], Object.getOwnPropertyDescriptor(functions, key) === undefined);
            Object.defineProperty(functions, key, { value: 3, writable: true, configurable: true });
            write(C, 4);
            console.log((C as any)[key], functions[key], objects[key]);
            delete (C as any)[key];
            delete functions[key];
            delete objects[key];
            """;
        Assert.Equal("true\nfalse 1 true\n4 3 1\n", TestHarness.Run(source, mode));
    }
}
