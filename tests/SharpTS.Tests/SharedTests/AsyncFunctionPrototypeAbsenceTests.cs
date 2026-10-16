using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncFunctionPrototypeAbsenceTests
{
    public const string IssueSource = """
        const expression: any = async function() {};
        async function declaration() {}
        const arrow: any = async () => {};
        for (const target of [expression, declaration as any, arrow]) {
            console.log(Object.hasOwn(target, "prototype"), typeof target.prototype);
            try { new target(); console.log("constructed"); }
            catch (error: any) { console.log(error.name, error instanceof TypeError); }
            try { console.log({} instanceof target); }
            catch (error: any) { console.log(error.name, error instanceof TypeError); }
        }
        """;

    public const string CapturedSource = """
        function make(offset: number) {
            const arrow: any = async (value: number) => offset + value;
            const expression: any = async function(value: number) { return offset + value; };
            return { arrow, expression };
        }
        class Owner {
            value = 30;
            run = async (value: number) => this.value + value;
        }
        const first: any = make(10), second: any = make(20), owner: any = new Owner();
        function rejectsConstruction(target: any) {
            try { new target(); return false; }
            catch (error) { return error instanceof TypeError; }
        }
        for (const target of [first.arrow, first.expression, second.arrow, second.expression, owner.run]) {
            console.log(Object.hasOwn(target, "prototype"), typeof target.prototype,
                rejectsConstruction(target));
        }
        console.log(first.arrow !== second.arrow, first.expression !== second.expression,
            owner.run === owner.run);
        async function run() {
            const a = await first.arrow(1);
            const b = await first.expression(2);
            const c = await second.arrow(3);
            const d = await second.expression(4);
            const e = await owner.run(5);
            console.log(a, b, c, d, e);
        }
        run();
        """;

    public const string PreservationSource = """
        function Ordinary(this: any) { this.value = 7; }
        const ordinaryExpression: any = function(this: any) { this.value = 8; };
        for (const target of [Ordinary as any, ordinaryExpression]) {
            const value = new target();
            console.log(Object.hasOwn(target, "prototype"), typeof target.prototype,
                value.value, value instanceof target);
        }
        function* generator() {}
        async function* asyncGenerator() {}
        const generatorExpression: any = function*() {};
        const asyncGeneratorExpression: any = async function*() {};
        function rejectsConstruction(target: any) {
            try { new target(); return false; }
            catch (error) { return error instanceof TypeError; }
        }
        for (const target of [generator as any, asyncGenerator as any,
            generatorExpression, asyncGeneratorExpression]) {
            console.log(Object.hasOwn(target, "prototype"), typeof target.prototype,
                rejectsConstruction(target), {} instanceof target);
        }
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource,
            "false undefined\nTypeError true\nTypeError true\nfalse undefined\nTypeError true\nTypeError true\nfalse undefined\nTypeError true\nTypeError true\n"];
        yield return ["captured", CapturedSource,
            "false undefined true\nfalse undefined true\nfalse undefined true\nfalse undefined true\nfalse undefined true\ntrue true true\n11 12 23 24 35\n"];
        yield return ["preservation", PreservationSource,
            "true object 7 true\ntrue object 8 true\ntrue object true false\ntrue object true false\ntrue object true false\ntrue object true false\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AsyncFunctionsHaveNoOwnPrototypeAndRemainNonConstructible(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
