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
        yield return ["default-membership-chain", """
            class Parent extends Array {}
            class Child extends Parent {}
            const values: any = new Child();
            console.log(Object.getPrototypeOf(values) === Child.prototype,
                Object.getPrototypeOf(Child.prototype) === Parent.prototype,
                Object.getPrototypeOf(Parent.prototype) === Array.prototype);
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object, Array.isArray(values));
            const structural: any = Object.create(Child.prototype);
            console.log(structural instanceof Child, structural instanceof Parent,
                structural instanceof Array, Array.isArray(structural));
            """, "true true true\ntrue true true true true\ntrue true true false\n"];
        yield return ["constructor-prototype-identity", """
            class Parent extends Array {}
            class Child extends Parent {}
            const values: any = new Child();
            console.log(Object.getPrototypeOf(Parent) === Array, Object.getPrototypeOf(Child) === Parent,
                Object.getPrototypeOf(Parent).prototype === Array.prototype,
                values instanceof Object.getPrototypeOf(Parent));
            """, "true true true true\n"];
        yield return ["live-prototype-membership", """
            class Parent extends Array {}
            class Child extends Parent {}
            const values: any = new Child();
            const original = Object.getPrototypeOf(Child.prototype);
            Object.setPrototypeOf(Child.prototype, null);
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object, Array.isArray(values));
            Object.setPrototypeOf(Child.prototype, original);
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object);
            Object.setPrototypeOf(values, {});
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object, Array.isArray(values));
            Object.setPrototypeOf(values, Child.prototype);
            console.log(values instanceof Child, values instanceof Parent,
                values instanceof Array, values instanceof Object);
            """, "true false false false true\ntrue true true true\nfalse false false true true\ntrue true true true\n"];
        yield return ["intrinsic-and-structural-membership", """
            class Values extends Array {}
            const hasInstance: any = (Function.prototype as any)[Symbol.hasInstance];
            const values: any = new Values();
            console.log(hasInstance.call(Values, values), hasInstance.call(Array, values));
            Object.setPrototypeOf(values, null);
            console.log(hasInstance.call(Values, values), hasInstance.call(Array, values));
            Object.setPrototypeOf(values, Array.prototype);
            console.log(hasInstance.call(Values, values), hasInstance.call(Array, values));
            console.log(Array.prototype instanceof Array, Array.isArray(Array.prototype));
            const structural: any = Object.create(Array.prototype);
            console.log(structural instanceof Array, Array.isArray(structural));
            const plain: any = [];
            Object.setPrototypeOf(plain, null);
            console.log(plain instanceof Array, plain instanceof Object, Array.isArray(plain));
            Object.setPrototypeOf(plain, Array.prototype);
            console.log(plain instanceof Array, plain instanceof Object);
            """, "true true\nfalse false\nfalse true\nfalse true\ntrue false\nfalse false true\ntrue true\n"];
        yield return ["arguments-membership", """
            function inspect() {
                console.log(Object.getPrototypeOf(arguments) === Object.prototype,
                    arguments instanceof Array, arguments instanceof Object, Array.isArray(arguments));
                Object.setPrototypeOf(arguments, Array.prototype);
                console.log(arguments instanceof Array, arguments instanceof Object, Array.isArray(arguments));
                Object.setPrototypeOf(arguments, null);
                console.log(arguments instanceof Array, arguments instanceof Object, Array.isArray(arguments));
            }
            inspect();
            """, "true false true false\ntrue true false\nfalse false false\n"];
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
            """, "true true\ntrue true\ntrue true true true\n"];
        yield return ["restored-prototype-properties", """
            class Child extends Array {}
            const values: any = new Child(1, 2);
            const original = Object.getPrototypeOf(values);
            Object.setPrototypeOf(values, null);
            Object.setPrototypeOf(values, original);
            console.log(values.push(3), values.length, values[2]);
            """, "3 3 3\n"];
        yield return ["array-prototype-custom-chain", """
            class Values extends Array {}
            const values: any = new Values(4, 5);
            const custom: any = Object.create(Array.prototype);
            custom.marker = 17;
            Object.setPrototypeOf(values, custom);
            console.log(values instanceof Values, values instanceof Array, values instanceof Object);
            """, "false true true\n"];
        yield return ["array-prototype-custom-properties", """
            class Values extends Array {}
            const values: any = new Values(4, 5);
            const custom: any = Object.create(Array.prototype);
            custom.marker = 17;
            Object.setPrototypeOf(values, custom);
            console.log(values.marker, values.push(6), values[2], Array.isArray(values));
            """, "17 3 6 true\n"];
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
            // Remaining interpreter exclusions cover inherited property reads (#2366).
            .Where(mode => mode == ExecutionMode.Compiled || (string)row[0] is not
                ("null-preserves-exotic-storage" or "custom-prototype-properties" or
                 "restored-prototype-properties" or "array-prototype-custom-properties" or "noncanonical-index-fields"))
            // Compiled counterparts are tracked independently in #2602, #2603 and #2605.
            .Where(mode => mode == ExecutionMode.Interpreted || (string)row[0] is not
                ("arguments-membership" or "live-prototype-membership" or "constructor-prototype-identity"))
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
