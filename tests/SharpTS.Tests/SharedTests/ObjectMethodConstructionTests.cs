using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ObjectMethodConstructionTests
{
    public const string IssueSource = """
        const object: any = { method() {} };
        const method: any = object.method;
        console.log("prototype" in method);
        try { new method(); console.log("constructed"); }
        catch (error) { console.log(error instanceof TypeError); }
        """;

    public const string MethodFormsSource = """
        function rejected(method: any) {
            let rejected = 0;
            try { new method(); } catch (error) { if (error instanceof TypeError) rejected++; }
            const bound: any = method.bind({}), rebound: any = bound.bind({});
            try { new bound(); } catch (error) { if (error instanceof TypeError) rejected++; }
            try { new rebound(); } catch (error) { if (error instanceof TypeError) rejected++; }
            try { Reflect.construct(method, []); } catch (error) { if (error instanceof TypeError) rejected++; }
            try { Reflect.construct(function() {}, [], method); }
            catch (error) { if (error instanceof TypeError) rejected++; }
            return rejected;
        }
        const key = Symbol("method");
        const object: any = { method() {}, "quoted"() {}, 1() {}, ["computed"]() {}, [key]() {} };
        for (const method of [object.method, object.quoted, object[1], object.computed, object[key]]) {
            console.log("prototype" in method, method.prototype === undefined,
                Object.getOwnPropertyDescriptor(method, "prototype") === undefined, rejected(method));
            method.prototype = {};
            console.log("prototype" in method, rejected(method));
        }
        """;

    public const string FunctionControlsSource = """
        const object: any = { method: function(this: any, value: number) { this.value = value; } };
        const expressed: any = object.method;
        function Declared(this: any, value: number) { this.value = value; }
        const declared: any = Declared;
        const bound: any = expressed.bind({});
        console.log("prototype" in expressed, "prototype" in declared,
            Object.getOwnPropertyDescriptor(expressed, "prototype") !== undefined);
        console.log(new expressed(7).value, new declared(8).value, new bound(9).value);
        """;

    public const string ReceiverSource = """
        function create(seed: number) {
            return {
                method(first: number, second: number = 2, ...rest: any[]) {
                    return [this, seed + first + second + rest.length, arguments.length];
                }
            };
        }
        const object: any = create(10), receiver: any = {};
        const direct: any = object.method(1, 2, 3);
        const borrowed: any = object.method.call(receiver, 4, 5, 6, 7);
        const bound: any = object.method.bind(receiver, 4);
        const boundResult: any = bound(5, 6, 7);
        console.log(direct[0] === object, direct[1], direct[2],
            borrowed[0] === receiver, borrowed[1], borrowed[2],
            boundResult[0] === receiver, boundResult[1], boundResult[2]);
        """;

    public const string AccessorSource = """
        const object: any = { get value() { return 1; }, set value(value: any) {} };
        const descriptor: any = Object.getOwnPropertyDescriptor(object, "value");
        for (const method of [descriptor.get, descriptor.set]) {
            console.log("prototype" in method, method.prototype === undefined,
                Object.getOwnPropertyDescriptor(method, "prototype") === undefined);
            try { new method(); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
            try { Reflect.construct(method, []); console.log(false); }
            catch (error) { console.log(error instanceof TypeError); }
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "false\ntrue\n"];
        yield return ["method-forms", MethodFormsSource,
            string.Concat(Enumerable.Repeat("false true true 5\ntrue 5\n", 5))];
        yield return ["function-controls", FunctionControlsSource, "true true true\n7 8 9\n"];
        yield return ["receiver", ReceiverSource, "true 14 3 true 21 4 true 21 4\n"];
        yield return ["accessors", AccessorSource,
            string.Concat(Enumerable.Repeat("false true true\ntrue\ntrue\n", 2))];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void MethodConstructionPolicyIsIndependentOfReceiverBinding(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
