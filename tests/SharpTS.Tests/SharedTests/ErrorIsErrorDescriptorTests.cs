using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public class ErrorIsErrorDescriptorTests
{
    [Theory, ModeData]
    public void IsErrorHasAnOwnStandardDataDescriptor(ExecutionMode mode)
    {
        const string source = """
            const descriptor = Object.getOwnPropertyDescriptor(Error, "isError");
            console.log(typeof descriptor.value, descriptor.value === Error.isError);
            console.log(descriptor.writable, descriptor.enumerable, descriptor.configurable);
            console.log(descriptor.value.length, descriptor.value.name);
            console.log(Error.hasOwnProperty("isError"), Error.propertyIsEnumerable("isError"));
            console.log(descriptor.value(new Error("message")), descriptor.value({}));
            """;
        Assert.Equal("function true\ntrue false true\n1 isError\ntrue false\ntrue false\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void IsErrorDescriptorSupportsRedefinitionAndDeletion(ExecutionMode mode)
    {
        const string source = """
            const saved = Error.isError;
            console.log(Object.getOwnPropertyDescriptor(TypeError, "isError") === undefined);
            Object.defineProperty(Error, "isError", { writable: false });
            console.log(Error.isError === saved);
            console.log(Object.getOwnPropertyDescriptor(Error, "isError").writable);
            Object.defineProperty(Error, "isError", { value: saved, writable: true });
            console.log(Error.isError(new Error()));
            console.log(delete (Error as any).isError);
            console.log((Error as any).isError === undefined);
            console.log(Object.getOwnPropertyDescriptor(Error, "isError") === undefined);
            """;
        Assert.Equal("true\ntrue\nfalse\ntrue\ntrue\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PartialDefinitionPreservesIntrinsicValueAndAttributes(ExecutionMode mode)
    {
        const string source = """
            const saved = Error.isError;
            Object.defineProperty(Error, "isError", { writable: false });
            const descriptor = Object.getOwnPropertyDescriptor(Error, "isError");
            console.log(descriptor.value === saved, descriptor.writable,
                descriptor.enumerable, descriptor.configurable);
            Object.defineProperty(Error, "isError", { writable: true });
            console.log(Error.isError === saved, Error.isError(new Error()));
            """;
        Assert.Equal("true false false true\ntrue true\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void CallsAndValueReadsObserveReplacementAndAccessorDefinitions(ExecutionMode mode)
    {
        const string source = """
            const alias: any = Error;
            function replacement(value: any) { return value === "replacement"; }
            Object.defineProperty(Error, "isError", { value: replacement });
            console.log(Error.isError === replacement, Error["isError"] === replacement,
                alias.isError === replacement);
            console.log(Error.isError("replacement"), Error["isError"]("replacement"),
                alias.isError(new Error()));
            let reads = 0;
            Object.defineProperty(Error, "isError", { get() { reads++; return replacement; } });
            console.log(Error.isError === replacement, Error.isError("replacement"), reads);
            """;
        Assert.Equal("true true true\ntrue true false\ntrue true 2\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void DeletedMethodIsAbsentAndNewDefinitionUsesNewPropertyDefaults(ExecutionMode mode)
    {
        const string source = """
            const saved = Error.isError;
            const alias: any = Error;
            console.log(delete alias.isError);
            console.log(Error.isError === undefined, Error["isError"] === undefined,
                alias.isError === undefined, typeof Error.isError);
            try { Error.isError(new Error()); } catch (error) { console.log(error instanceof TypeError); }
            Object.defineProperty(Error, "isError", { value: saved });
            const descriptor = Object.getOwnPropertyDescriptor(Error, "isError");
            console.log(descriptor.value === saved, descriptor.writable,
                descriptor.enumerable, descriptor.configurable);
            console.log(Error.isError(new Error()), Error.hasOwnProperty("isError"));
            """;
        Assert.Equal("true\ntrue true true undefined\ntrue\ntrue false false false\ntrue true\n",
            TestHarness.Run(source, mode));
    }
}
