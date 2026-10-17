using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ConstrainedGenericSymbolLookupTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["method", """
            const key = Symbol("method");
            class Marker { marker = 1; }
            class C<T extends Marker> { static [key]() { return this; } }
            const owner: any = C;
            console.log(owner[key]() === owner);
            const method = owner[key];
            console.log(method === owner[key], method.call(owner) === owner);
            """, "true\ntrue true\n"];
        yield return ["multiple-parameters", """
            const key = Symbol("method");
            class Marker { marker = 1; }
            class C<T extends Marker, U extends T, V> { static [key]() { return this; } }
            const owner: any = C;
            console.log(owner[key]() === owner, owner[key]() === owner);
            """, "true true\n"];
        yield return ["inherited-open-and-closed", """
            const key = Symbol("method");
            class Marker { marker = 1; }
            class Base<T extends Marker> { static [key]() { return this; } }
            class Child<U extends Marker> extends Base<U> {}
            class Closed extends Base<Marker> {}
            for (const owner of [Base, Child, Closed] as any[]) {
                console.log(owner[key]() === owner);
            }
            """, "true\ntrue\ntrue\n"];
        yield return ["accessors", """
            const key = Symbol("accessor");
            class Marker { marker = 1; }
            class C<T extends Marker> {
                static stored = 3;
                static get [key](): any { return this; }
                static set [key](value: any) { C.stored = value; }
            }
            const owner: any = C;
            console.log(owner[key] === owner);
            owner[key] = 7;
            console.log(C.stored, owner[key] === owner);
            """, "true\n7 true\n"];
    }

    public static IEnumerable<object[]> ParityCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void SymbolLookupRespectsGenericOwnerConstraints(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
