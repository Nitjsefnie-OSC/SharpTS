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

    // Compiled intrinsic descriptor mutation is tracked separately in #2003.
    [Theory, InterpretedOnlyData]
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
}
