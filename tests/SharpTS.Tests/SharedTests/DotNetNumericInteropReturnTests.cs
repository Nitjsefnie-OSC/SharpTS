using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DotNetNumericInteropReturnTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["typed-dynamic-and-wrapped-properties", """
            import { StringBuilder } from "dotnet:System.Text.StringBuilder";
            interface Application { readonly windowCount: number; }
            const builder = new StringBuilder("abc");
            const managed: any = builder;
            const state = { reads: 0 };
            const application: Application = {
                get windowCount(): number { state.reads++; return managed.length; }
            };
            function compare(value: any) {
                console.log(typeof value, value === 3, value !== 3, value == 3, value != 3,
                    3 === value, 3 == value);
            }
            compare(builder.length); compare(managed.length); compare(application.windowCount);
            console.log(builder.length === 3, managed.length === 3, application.windowCount === 3, state.reads);
            function raw(value: number = 1) { console.log(typeof value, value === 0, value === null); }
            const invoke: any = raw;
            invoke(null); invoke(false); invoke("0"); invoke({});
            """, "number true false true false true true\nnumber true false true false true true\nnumber true false true false true true\ntrue true true 2\nobject false true\nboolean false false\nstring false false\nobject false false\n"];
        yield return ["typed-numeric-method-results", """
            import { Convert } from "dotnet:System.Convert";
            function compare(value: any) { console.log(typeof value, value === 3, value == 3); }
            compare(Convert.toInt32(3));
            compare(Convert.toInt64(3));
            compare(Convert.toSingle(3));
            compare(Convert.toDecimal(3));
            console.log(typeof Convert.toBoolean(1), Convert.toBoolean(1) === true,
                typeof Convert.toString(3), Convert.toString(3) === "3");
            """, "number true true\nnumber true true\nnumber true true\nnumber true true\nboolean true string true\n"];
        yield return ["dynamic-method-wrapper-and-object-payload", """
            import { MemoryStream } from "dotnet:System.IO.MemoryStream";
            import { Convert, Int32, TypeCode } from "dotnet:System";
            interface Reading { readonly value: number; }
            const stream = new MemoryStream();
            const managed: any = stream;
            const reading: Reading = { get value(): number { return managed.readByte(); } };
            function compare(value: any) {
                console.log(typeof value, value === -1, value !== -1, value == -1, -1 === value);
            }
            compare(stream.readByte()); compare(managed.readByte()); compare(reading.value);
            const extracted: any = managed.readByte;
            compare(extracted());
            const boxed: any = Convert.changeType("3", TypeCode.Int32);
            console.log(typeof boxed, boxed === 3, boxed == 3);
            console.log(typeof Int32.maxValue, Int32.maxValue === 2147483647, Int32.maxValue == 2147483647);
            const boolean: any = Convert.changeType("true", TypeCode.Boolean);
            const text: any = Convert.changeType(3, TypeCode.String);
            const nil: any = Convert.changeType(null, TypeCode.String);
            console.log(typeof boolean, boolean === true, typeof text, text === "3", nil === null);
            """, "number true false true true\nnumber true false true true\nnumber true false true true\nnumber true false true true\nnumber true true\nnumber true true\nboolean true string true true\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ClrNumericReturnsBecomeGuestNumbers(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(new Dictionary<string, string> { ["./main.ts"] = source }, "./main.ts", mode));
    }
}
