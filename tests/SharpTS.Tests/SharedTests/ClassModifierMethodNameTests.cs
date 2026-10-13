using SharpTS.Tests.Infrastructure;
using SharpTS.Parsing;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassModifierMethodNameTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", "class C { private() { return 42; } } console.log(new C().private());", "42\n"];
        yield return ["declaration", """
            class C {
                public() { return 1; } protected() { return 2; }
                static() { return 3; } readonly() { return 4; }
                abstract() { return 5; } override() { return 6; }
                async() { return 7; } declare() { return 8; }
            }
            const c = new C();
            console.log(c.public(), c.protected(), c.static(), c.readonly(),
                c.abstract(), c.override(), c.async(), c.declare());
            """, "1 2 3 4 5 6 7 8\n"];
        yield return ["expression", """
            const C = class { private() { return 42; } protected() { return 43; } async() { return 44; } };
            const c = new C();
            console.log(c.private(), c.protected(), c.async());
            """, "42 43 44\n"];
        yield return ["generic-static", """
            class C {
                static private() { return 41; }
                private<T>(value: T): T { return value; }
            }
            const E = class { private<T>(value: T): T { return value; } };
            console.log(C.private(), new C().private<number>(42), new E().private<string>("named"));
            """, "41 42 named\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void ModifierWordsCanNameMethods(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }

    [Fact]
    public void ActualModifiersAndStaticBlocksRetainTheirMeaning()
    {
        var source = """
            class C {
                private hidden() {} protected guarded() {} static exposed() {}
                readonly value = 1;
                static {}
            }
            """;
        var cls = Assert.IsType<Stmt.Class>(Assert.Single(TestHarness.ParseOrThrow(source)));
        Assert.Equal(AccessModifier.Private, cls.Methods.Single(method => method.Name.Lexeme == "hidden").Access);
        Assert.Equal(AccessModifier.Protected, cls.Methods.Single(method => method.Name.Lexeme == "guarded").Access);
        Assert.True(cls.Methods.Single(method => method.Name.Lexeme == "exposed").IsStatic);
        Assert.True(Assert.Single(cls.Fields).IsReadonly);
        Assert.IsType<Stmt.StaticBlock>(Assert.Single(cls.StaticInitializers!));
    }
}
