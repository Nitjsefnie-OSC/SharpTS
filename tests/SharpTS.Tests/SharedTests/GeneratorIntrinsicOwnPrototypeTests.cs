using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GeneratorIntrinsicOwnPrototypeTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            function* generator() { yield 1; }
            async function* asyncGenerator() { yield 1; }
            for (const f of [generator, asyncGenerator]) {
                console.log(Object.hasOwn(f, 'prototype'), typeof (f as any).prototype);
            }
            """, "true object\ntrue object\n"];
        yield return ["own-descriptor-reflection-and-identity", """
            function* declared() { yield 1; }
            async function* asyncDeclared() { yield 2; }
            const expression: any = function*() { yield 3; };
            const asyncExpression: any = async function*() { yield 4; };
            class C { *g() { yield 5; } async *ag() { yield 6; } }
            const instance: any = new C();
            for (const f of [declared, asyncDeclared, expression, asyncExpression, instance.g, instance.ag]) {
                const descriptor: any = Object.getOwnPropertyDescriptor(f, "prototype");
                const aggregate: any = Object.getOwnPropertyDescriptors(f);
                console.log(Object.hasOwn(f, "prototype"), descriptor.value === f.prototype,
                    descriptor.writable, descriptor.enumerable, descriptor.configurable,
                    Object.getOwnPropertyNames(f).includes("prototype"),
                    Reflect.ownKeys(f).includes("prototype"), aggregate.prototype.value === f.prototype,
                    Object.keys(f).includes("prototype"));
            }
            console.log(declared.prototype !== asyncDeclared.prototype,
                instance.g.prototype === instance.g.prototype);
            """, "true true true false false true true true false\ntrue true true false false true true true false\ntrue true true false false true true true false\ntrue true true false false true true true false\ntrue true true false false true true true false\ntrue true true false false true true true false\ntrue true\n"];
        yield return ["prototype-property-mutation-rules", """
            function* declared() {}
            async function* asyncDeclared() {}
            const expression: any = function*() {};
            const asyncExpression: any = async function*() {};
            function strictWrite(f: any, value: any): boolean {
                "use strict";
                try { f.prototype = value; return false; }
                catch (error) { return error instanceof TypeError; }
            }
            const functions: any[] = [declared, asyncDeclared, expression, asyncExpression];
            for (const f of functions) {
                const replacement = {};
                console.log(Reflect.deleteProperty(f, "prototype"), delete f["prototype"],
                    Reflect.set(f, "prototype", replacement), f.prototype === replacement);
                console.log(Reflect.defineProperty(f, "prototype", { enumerable: true }),
                    Reflect.defineProperty(f, "prototype", { configurable: true }));
                Object.defineProperty(f, "prototype", { writable: false });
                console.log(Reflect.set(f, "prototype", {}), strictWrite(f, {}),
                    Reflect.defineProperty(f, "prototype", { writable: true }),
                    Object.getOwnPropertyDescriptor(f, "prototype")!.value === replacement);
            }
            """, "false false true true\nfalse false\nfalse true false true\nfalse false true true\nfalse false\nfalse true false true\nfalse false true true\nfalse false\nfalse true false true\nfalse false true true\nfalse false\nfalse true false true\n"];
        yield return ["live-iterator-prototype-and-primitive-fallback", """
            function make(): any[] {
                const captured = 3;
                return [function*() { yield captured; }, async function*() { yield captured; }];
            }
            const first = make(), second = make();
            for (let index = 0; index < first.length; index++) {
                const f: any = first[index], other: any = second[index];
                const original = f.prototype, intrinsic = Object.getPrototypeOf(original);
                const earlier = f();
                const custom: any = Object.create(intrinsic); custom.marker = "custom";
                f.prototype = custom;
                const later = f();
                let fallback = true;
                for (const value of [null, undefined, 7, "text", true, 1n, Symbol("primitive")]) {
                    f.prototype = value;
                    fallback = fallback && Object.getPrototypeOf(f()) === intrinsic
                        && Object.hasOwn(f, "prototype") && f.prototype === value;
                }
                console.log(original !== other.prototype, Object.getPrototypeOf(earlier) === original,
                    Object.getPrototypeOf(later) === custom, later.marker, fallback);
            }
            console.log(Object.getPrototypeOf(second[0].prototype) !== Object.getPrototypeOf(second[1].prototype));
            function makeFresh(): any { const value = 7; return function*() { yield value; }; }
            const firstFunction: any = makeFresh(), secondFunction: any = makeFresh();
            console.log(firstFunction.prototype === firstFunction.prototype,
                firstFunction.prototype !== secondFunction.prototype);
            """, "true true true custom true\ntrue true true custom true\ntrue\ntrue true\n"];
        yield return ["nonconstructible-despite-own-prototype", """
            function* declared() {}
            async function* asyncDeclared() {}
            const expression: any = function*() {};
            const asyncExpression: any = async function*() {};
            function Target() {}
            function rejects(run: () => any): boolean {
                try { run(); return false; } catch (error) { return error instanceof TypeError; }
            }
            for (const f of [declared, asyncDeclared, expression, asyncExpression]) {
                const generator: any = f, bound: any = f.bind(null);
                console.log(rejects(() => new generator()), rejects(() => new bound()),
                    rejects(() => Reflect.construct(generator, [])),
                    rejects(() => Reflect.construct(bound, [])),
                    rejects(() => Reflect.construct(Target, [], generator)),
                    rejects(() => Reflect.construct(Target, [], bound)));
            }
            """, "true true true true true true\ntrue true true true true true\ntrue true true true true true\ntrue true true true true true\n"];
    }

    // Full iterator/function intrinsic chains remain interpreter-only (#2139).
    public static IEnumerable<object[]> CompiledCases() => Cases()
        .Where(row => (string)row[0] != "live-iterator-prototype-and-primitive-fallback");

    public static IEnumerable<object[]> ModeCases() => Cases()
        .Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Interpreted })
        .Concat(CompiledCases().Select(row => new object[] { row[0], row[1], row[2], ExecutionMode.Compiled }));

    [Theory, MemberData(nameof(ModeCases))]
    public void GeneratorOwnPrototypesRemainSeparateFromConstructorCapability(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
