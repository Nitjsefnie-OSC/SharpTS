using SharpTS.Tests.Infrastructure;
using SharpTS.Execution;
using SharpTS.Parsing;
using SharpTS.Runtime;
using SharpTS.Runtime.BuiltIns;
using SharpTS.Runtime.Exceptions;
using SharpTS.Runtime.Types;
using SharpTS.TypeSystem;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassMethodPrototypeTests
{
    public const string IssueSource = """
        class C { method() {} static staticMethod() {} }
        console.log('prototype' in C.prototype.method, 'prototype' in C.staticMethod);
        """;

    public const string MethodsSource = """
        const key = Symbol("method");
        class C {
            method() {} static staticMethod() {}
            async asynchronous() {} static async staticAsynchronous() {}
            [key]() {} static [key]() {}
        }
        const E: any = class {
            method() {} static staticMethod() {}
            async asynchronous() {} static async staticAsynchronous() {}
            [key]() {} static [key]() {}
        };
        const ctor: any = C, instance: any = new C(), expression: any = new E();
        for (const method of [instance.method, ctor.staticMethod, instance.asynchronous,
            ctor.staticAsynchronous, instance[key], ctor[key], expression.method,
            E.staticMethod, expression.asynchronous, E.staticAsynchronous, expression[key], E[key]]) {
            console.log('prototype' in method, method.prototype === undefined,
                Object.getOwnPropertyDescriptor(method, "prototype") === undefined);
            try { new method(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            const bound: any = method.bind({});
            try { new bound(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.construct(method, []); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.construct(function() {}, [], method); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
        }
        """;

    public const string AccessorsSource = """
        class C { static get value() { return 1; } static set value(value: any) {} }
        const E: any = class { static get value() { return 1; } static set value(value: any) {} };
        const declared: any = Object.getOwnPropertyDescriptor(C, "value");
        const expression: any = Object.getOwnPropertyDescriptor(E, "value");
        for (const method of [declared.get, declared.set, expression.get, expression.set]) {
            console.log('prototype' in method, method.prototype === undefined);
            try { new method(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.construct(method, []); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
        }
        """;

    public const string AssignedPrototypeSource = """
        class C { method() {} static staticMethod() {} }
        const instance: any = new C(), ctor: any = C;
        for (const method of [instance.method, ctor.staticMethod]) {
            method.prototype = {};
            console.log('prototype' in method, typeof method.prototype);
            try { new method(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            const bound: any = method.bind({});
            try { new bound(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.construct(method, []); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
        }
        """;

    public const string FunctionControlsSource = """
        function Declared() {}
        const expressed: any = function() {};
        const declared: any = Declared;
        console.log('prototype' in declared, 'prototype' in expressed);
        console.log(new declared() instanceof declared, new expressed() instanceof expressed);
        const bound: any = declared.bind({});
        console.log(new bound() instanceof declared);
        // Reflect.construct result identity has a separate interpreter defect (#2060).
        try { Reflect.construct(declared, []); console.log(true); }
        catch (error) { console.log(false); }
        const arrow: any = () => {};
        console.log('prototype' in arrow);
        try { new arrow(); console.log(false); }
        catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string GeneratorControlsSource = """
        class C {
            *generator() {} static *staticGenerator() {}
            async *asyncGenerator() {} static async *staticAsyncGenerator() {}
        }
        const E: any = class {
            *generator() {} static *staticGenerator() {}
            async *asyncGenerator() {} static async *staticAsyncGenerator() {}
        };
        const ctor: any = C, instance: any = new C(), expression: any = new E();
        for (const method of [instance.generator, ctor.staticGenerator,
            instance.asyncGenerator, ctor.staticAsyncGenerator, expression.generator,
            E.staticGenerator, expression.asyncGenerator, E.staticAsyncGenerator]) {
            console.log('prototype' in method, typeof method.prototype);
            try { new method(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.construct(method, []); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            const bound: any = method.bind({});
            try { new bound(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "false false\n"];
        yield return ["methods", MethodsSource,
            string.Concat(Enumerable.Repeat("false true true\ntrue\ntrue\ntrue\ntrue\n", 12))];
        yield return ["assigned-prototype", AssignedPrototypeSource,
            string.Concat(Enumerable.Repeat("true object\ntrue\ntrue\ntrue\n", 2))];
        yield return ["function-controls", FunctionControlsSource,
            "true true\ntrue true\ntrue\ntrue\nfalse\ntrue\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void OrdinaryClassMethodsHaveNoIntrinsicPrototype(
        string source, string expected, ExecutionMode mode)
        => Assert.Equal(expected, TestHarness.Run(source, mode));

    [Fact]
    public void CompiledAccessorValuesRejectConstructionWithoutPrototype()
        => Assert.Equal(string.Concat(Enumerable.Repeat("false true\ntrue\ntrue\n", 4)),
            TestHarness.Run(AccessorsSource, ExecutionMode.Compiled));

    [Fact]
    public void InterpreterAccessorValuesAndBoundViewsRejectConstructionWithoutPrototype()
    {
        // Class prototype accessor descriptors are omitted by the guest reflection path (#2021).
        // Read the actual accessor values from evaluated classes, then exercise Get/Construct.
        const string source = """
            const key = Symbol("accessor");
            class C {
                get value() { return 1; } set value(value: any) {}
                static get value() { return 1; } static set value(value: any) {}
                get [key]() { return 1; } set [key](value: any) {}
                static get [key]() { return 1; } static set [key](value: any) {}
            }
            const E = class {
                get value() { return 1; } set value(value: any) {}
                static get value() { return 1; } static set value(value: any) {}
                get [key]() { return 1; } set [key](value: any) {}
                static get [key]() { return 1; } static set [key](value: any) {}
            };
            inspect(C, key); inspect(E, key);
            """;
        using var interpreter = new Interpreter(stdout: TextWriter.Null, stderr: TextWriter.Null);
        var inspected = 0;
        interpreter.RegisterGlobal("inspect", BuiltInMethod.CreateV2("inspect", 2,
            (runtime, _, arguments) =>
            {
                var klass = Assert.IsType<SharpTSClass>(arguments[0].ToObject());
                var key = Assert.IsType<SharpTSSymbol>(arguments[1].ToObject());
                SharpTSFunction?[] accessors = [klass.FindGetter("value"), klass.FindSetter("value"),
                    klass.FindStaticGetter("value"), klass.FindStaticSetter("value"),
                    klass.FindSymbolGetter(key), klass.FindSymbolSetter(key),
                    klass.FindStaticSymbolGetter(key), klass.FindStaticSymbolSetter(key)];
                foreach (var accessor in accessors)
                {
                    Assert.NotNull(accessor);
                    foreach (var function in new[] { accessor, accessor.BindStatic(klass),
                        accessor.Bind(new SharpTSInstance(klass)), accessor.BindThis(new SharpTSObject([])) })
                    {
                        Assert.IsType<SharpTSUndefined>(runtime.GetPropertyValue(function, "prototype"));
                        var thrown = Assert.Throws<ThrowException>(() => runtime.Construct(function, []));
                        Assert.IsType<SharpTSTypeError>(thrown.Value);
                    }
                }
                inspected++;
                return RuntimeValue.Undefined;
            }));
        var statements = new Parser(new Lexer(source).ScanTokens()).ParseOrThrow();
        interpreter.Interpret(statements, new TypeMap());
        Assert.Null(interpreter.LastUncaughtError);
        Assert.Equal(2, inspected);
    }

    [Fact]
    public void CompiledGeneratorMethodsKeepPrototypeWithoutConstruction()
    {
        // Interpreter generator functions currently lack their own prototype property (#2059).
        Assert.Equal(string.Concat(Enumerable.Repeat("true object\ntrue\ntrue\ntrue\n", 8)),
            TestHarness.Run(GeneratorControlsSource, ExecutionMode.Compiled));
    }
}
