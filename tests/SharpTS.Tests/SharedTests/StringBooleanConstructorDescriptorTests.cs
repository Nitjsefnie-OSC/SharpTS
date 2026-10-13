using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class StringBooleanConstructorDescriptorTests
{
    public const string IssueSource = """
        Object.defineProperty(String, "custom", { value: 1 });
        Object.defineProperty(Boolean, "custom", { value: 1 });
        console.log(Object.hasOwn(String, "custom"), Object.hasOwn(Boolean, "custom"));
        """;

    public const string NativeSource = """
        const constructor: any = CONSTRUCTOR, prototype = CONSTRUCTOR.prototype;
        const name = Object.getOwnPropertyDescriptor(constructor, "name")!;
        const length = Object.getOwnPropertyDescriptor(constructor, "length")!;
        const proto = Object.getOwnPropertyDescriptor(constructor, "prototype")!;
        console.log(name.value, name.writable, name.enumerable, name.configurable,
            length.value, length.writable, length.enumerable, length.configurable);
        console.log(proto.value === prototype, proto.writable, proto.enumerable, proto.configurable,
            Reflect.getOwnPropertyDescriptor(constructor, "prototype")!.value === prototype);
        console.log(Reflect.defineProperty(constructor, "prototype", { value: prototype }),
            Reflect.defineProperty(constructor, "prototype", { value: {} }),
            Reflect.deleteProperty(constructor, "prototype"), constructor.prototype === prototype);
        Object.defineProperty(constructor, "name", { value: "Changed", writable: true });
        constructor["name"] = "Assigned";
        Object.defineProperty(constructor, "length", { value: 2 });
        console.log(constructor.name, constructor.length,
            Object.getOwnPropertyDescriptor(constructor, "name")!.configurable,
            Object.getOwnPropertyDescriptor(constructor, "length")!.configurable);
        console.log(Object.getOwnPropertyNames(constructor).includes("prototype"),
            Reflect.ownKeys(constructor).includes("name"),
            Object.getOwnPropertyDescriptors(constructor).name.value);
        console.log(delete constructor.name, Object.hasOwn(constructor, "name"),
            Object.getOwnPropertyDescriptor(constructor, "name") === undefined);
        """;

    public const string StringMethodsSource = """
        const constructor: any = String, method = String.fromCharCode;
        const descriptor = Object.getOwnPropertyDescriptor(constructor, "fromCharCode")!;
        console.log(method === String.fromCharCode, descriptor.value === method,
            descriptor.writable, descriptor.enumerable, descriptor.configurable, method(65));
        Object.defineProperty(constructor, "fromCharCode", { enumerable: true });
        const updated = Reflect.getOwnPropertyDescriptor(constructor, "fromCharCode")!;
        console.log(updated.value === method, updated.writable, updated.enumerable, updated.configurable,
            Object.keys(constructor).includes("fromCharCode"), constructor.fromCharCode(66));
        console.log(delete constructor.fromCharCode, Object.hasOwn(constructor, "fromCharCode"),
            constructor.fromCharCode === undefined);
        Object.defineProperty(constructor, "fromCharCode", { value: () => "changed", configurable: true });
        console.log(constructor.fromCharCode(), method(67));
        """;

    public static IEnumerable<object[]> Cases()
    {
        foreach (string constructor in new[] { "String", "Boolean" })
        {
            foreach (object[] row in NumberConstructorDescriptorTests.Cases()
                         .Where(row => (string)row[0] != "intrinsics"))
            {
                var modes = (string)row[0] is "issue" or "data" or "invariants"
                    ? new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
                    : new[] { ExecutionMode.Interpreted };
                foreach (ExecutionMode mode in modes)
                    yield return [constructor + " " + row[0],
                        ((string)row[1]).Replace("Number", constructor), row[2], mode];
            }
            yield return [constructor + " native", NativeSource.Replace("CONSTRUCTOR", constructor),
                constructor + " false false true 1 false false true\ntrue false false false true\n"
                    + "true false false true\nAssigned 2 true true\ntrue true Assigned\ntrue false true\n",
                ExecutionMode.Interpreted];
        }
        yield return ["String methods", StringMethodsSource,
            "true true true false true A\ntrue true true true true B\ntrue false true\nchanged C\n",
            ExecutionMode.Interpreted];
    }

    [Theory, MemberData(nameof(Cases))]
    public void ConstructorDefinitionsPreserveDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Theory]
    [InlineData("String")]
    [InlineData("Boolean")]
    public void DescriptorMutationsStayWithinTheirRealm(string constructor)
    {
        const string first = """
            const constructor: any = CONSTRUCTOR;
            Object.defineProperty(constructor, "realmOnly", { value: 7 });
            Object.defineProperty(constructor, "name", { value: "Changed" });
            console.log(constructor.realmOnly, constructor.name);
            """;
        const string second = """
            const constructor: any = CONSTRUCTOR;
            console.log(Object.hasOwn(constructor, "realmOnly"), constructor.realmOnly === undefined,
                constructor.name, Object.getOwnPropertyDescriptor(constructor, "prototype")!.value === constructor.prototype);
            """;
        Assert.Equal("7 Changed\n", TestHarness.RunInterpreted(first.Replace("CONSTRUCTOR", constructor)));
        Assert.Equal("false true " + constructor + " true\n",
            TestHarness.RunInterpreted(second.Replace("CONSTRUCTOR", constructor)));
    }
}
