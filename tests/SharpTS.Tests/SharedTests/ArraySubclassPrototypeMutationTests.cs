using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ArraySubclassPrototypeMutationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original-null-membership", """
            class ArrayParent extends Array {}
            class ArrayChild extends ArrayParent {}
            const array: any = new ArrayChild();
            Object.setPrototypeOf(array, null);
            console.log(array instanceof ArrayChild, array instanceof ArrayParent,
                array instanceof Array, array instanceof Object);
            """, "false false false false\n"];
        yield return ["null-preserves-exotic-storage", """
            class Values extends Array { tag = 'own'; read() { return this.tag; } }
            const values: any = new Values(3, 4);
            Object.setPrototypeOf(values, null);
            console.log(Array.isArray(values), values.length, values[0], values.tag);
            console.log(typeof values.push, typeof values.read);
            values[1] = 9;
            values.length = 1;
            console.log(values.length, values[0], 1 in values);
            console.log(Array.prototype.push.call(values, 7), values.length, values[1]);
            """, "true 2 3 own\nundefined undefined\n1 3 false\n2 2 7\n"];
        yield return ["custom-prototype-properties", """
            class Values extends Array { old() { return 'old'; } }
            const values: any = new Values(7, 8);
            const custom: any = {
                marker: 'custom',
                get first() { return (this as any)[0]; },
                read() { return (this as any)[1]; }
            };
            console.log(Object.setPrototypeOf(values, custom) === values);
            console.log(Object.getPrototypeOf(values) === custom, values.marker, values.first, values.read());
            console.log(typeof values.push, typeof values.old, Array.isArray(values));
            """, "true\ntrue custom 7 8\nundefined undefined true\n"];
        yield return ["reflect-and-restored-membership", """
            class Parent extends Array {}
            class Child extends Parent {}
            const values: any = new Child(1, 2);
            const original = Object.getPrototypeOf(values);
            console.log(Reflect.setPrototypeOf(values, null), Object.getPrototypeOf(values) === null);
            console.log(Reflect.setPrototypeOf(values, original), Object.getPrototypeOf(values) === original);
            console.log(values instanceof Child, values instanceof Parent, values instanceof Array, values instanceof Object);
            console.log(values.push(3), values.length, values[2]);
            """, "true true\ntrue true\ntrue true true true\n3 3 3\n"];
        yield return ["array-prototype-custom-chain", """
            class Values extends Array {}
            const values: any = new Values(4, 5);
            const custom: any = Object.create(Array.prototype);
            custom.marker = 17;
            Object.setPrototypeOf(values, custom);
            console.log(values instanceof Values, values instanceof Array, values instanceof Object);
            console.log(values.marker, values.push(6), values[2], Array.isArray(values));
            """, "false true true\n17 3 6 true\n"];
        yield return ["nonextensible-same-prototype", """
            class Values extends Array {}
            const values: any = new Values(1, 2);
            const original = Object.getPrototypeOf(values);
            Object.preventExtensions(values);
            console.log(Object.setPrototypeOf(values, original) === values, Reflect.setPrototypeOf(values, original));
            console.log(Reflect.setPrototypeOf(values, null));
            try { Object.setPrototypeOf(values, null); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(values) === original, values.length, values[0]);
            """, "true true\nfalse\ntrue\ntrue 2 1\n"];
        yield return ["noncanonical-index-fields", """
            class Values extends Array {
                '01' = 'own';
                '+0' = 'plus';
                '-0' = 'minus';
                '4294967295' = 'max';
            }
            const values: any = new Values(10, 20);
            console.log(values[0], values[1], values['01'], values['+0'], values['-0'], values['4294967295']);
            Object.setPrototypeOf(values, null);
            console.log(values[0], values[1], values['01'], values['+0'], values['-0'], values['4294967295']);
            """, "10 20 own plus minus max\n10 20 own plus minus max\n"];
        yield return ["cycle-rejection", """
            class Values extends Array {}
            const values: any = new Values();
            const original = Object.getPrototypeOf(values);
            console.log(Reflect.setPrototypeOf(values, values));
            const custom: any = {};
            Object.setPrototypeOf(custom, values);
            console.log(Reflect.setPrototypeOf(values, custom));
            try { Object.setPrototypeOf(values, custom); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(values) === original);
            """, "false\nfalse\ntrue\ntrue\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            // Interpreter membership and prototype lookup are tracked separately in #2365/#2366.
            .Where(mode => mode == ExecutionMode.Compiled || (string)row[0] is
                ("nonextensible-same-prototype" or "cycle-rejection"))
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ArrayStorageSurvivesLegalPrototypeMutation(string name, string source, string expected, ExecutionMode mode)
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
