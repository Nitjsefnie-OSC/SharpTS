using SharpTS.Tests.Infrastructure;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassOverrideValidationTests
{
    public static IEnumerable<object[]> ClassFormsAndModes()
    {
        foreach (bool expression in new[] { false, true })
            foreach (ExecutionMode mode in new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
                yield return [expression, mode];
    }

    [Theory, MemberData(nameof(ClassFormsAndModes))]
    public void EitherBaseAccessorFacetCanBeOverridden(bool expression, ExecutionMode mode)
    {
        string source = """
            class SetterBase { set value(next: number) {} }
            class GetterBase { get value(): number { return 1; } }
            """ + Derived(expression, "GetterChild", "SetterBase",
                "override get value(): number { return 2; }")
            + Derived(expression, "SetterChild", "GetterBase",
                "stored = 0; override set value(next: number) { this.stored = next; }")
            + """
            const child = new SetterChild();
            child.value = 3;
            console.log(new GetterChild().value, child.stored);
            """;
        Assert.Equal("2 3\n", TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ClassFormsAndModes))]
    public void GenericInstanceOverridesValidateKnownBaseMembers(bool expression, ExecutionMode mode)
    {
        string source = "class Base { value(): number { return 1; } }"
            + Derived(expression, "Derived", "Base", "override value(): number { return 2; }", generic: true)
            + "console.log(new Derived<number>().value());";
        Assert.Equal("2\n", TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ClassFormsAndModes))]
    public void FreshSingletonMethodAndGetterReturnsWiden(bool expression, ExecutionMode mode)
    {
        string source = """
            const numberValue = 2;
            const stringValue = "child";
            const booleanValue = false;
            const bigintValue = 2n;
            class Base {
                static number() { return 1; }
                static string() { return "base"; }
                static boolean() { return true; }
                static bigint() { return 1n; }
                static get numberProperty() { return 1; }
                static get stringProperty() { return "base"; }
                static get booleanProperty() { return true; }
                static get bigintProperty() { return 1n; }
            }
            """ + Derived(expression, "Derived", "Base", """
                static override number() { return numberValue; }
                static override string() { return stringValue; }
                static override boolean() { return booleanValue; }
                static override bigint() { return bigintValue; }
                static override get numberProperty() { return 2; }
                static override get stringProperty() { return "child"; }
                static override get booleanProperty() { return false; }
                static override get bigintProperty() { return 2n; }
                """) + """
            console.log(Derived.number(), Derived.string(), Derived.boolean(), String(Derived.bigint()));
            console.log(Derived.numberProperty, Derived.stringProperty, Derived.booleanProperty, String(Derived.bigintProperty));
            """;
        Assert.Equal("2 child false 2\n2 child false 2\n", TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ClassFormsAndModes))]
    public void FixedLiteralAndUnionReturnsKeepTheirSignatures(bool expression, ExecutionMode mode)
    {
        string source = """
            function fixedLiteral(): 1 { return 1; }
            class Base {
                static asserted(): 1 { return 1; }
                static annotatedAlias(): 1 { return 1; }
                static assertedAlias(): 1 { return 1; }
                static fixedCall(): 1 { return 1; }
                static choice(flag: boolean): 1 | 2 { return 1; }
                static get property(): 1 { return 1; }
            }
            """ + Derived(expression, "Derived", "Base", """
                static override asserted() { return 1 as const; }
                static override annotatedAlias() { const value: 1 = 1; return value; }
                static override assertedAlias() { const value = 1 as const; return value; }
                static override fixedCall() { return fixedLiteral(); }
                static override choice(flag: boolean) { if (flag) return 1; return 2; }
                static override get property() { return 1 as const; }
                """) + """
            const literal: 1 = Derived.asserted();
            const choice: 1 | 2 = Derived.choice(false);
            console.log(literal, Derived.annotatedAlias(), Derived.assertedAlias(), Derived.fixedCall(), choice, Derived.property);
            """;
        Assert.Equal("1 1 1 1 2 1\n", TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ClassFormsAndModes))]
    public void AsyncSingletonReturnsWidenBeforePromiseWrapping(bool expression, ExecutionMode mode)
    {
        string source = "class Base { static async value() { return 1; } }"
            + Derived(expression, "Derived", "Base", "static override async value() { return 2; }")
            + "async function main() { console.log(await Derived.value()); } main();";
        Assert.Equal("2\n", TestHarness.Run(source, mode));
    }

    [Theory, MemberData(nameof(ClassFormsAndModes))]
    public void WellKnownSymbolAccessorsUseTheirKnownMemberNames(bool expression, ExecutionMode mode)
    {
        string source = """
            class Base {
                set [Symbol.toStringTag](next: string) {}
                static get [Symbol.toStringTag](): string { return "base"; }
            }
            """ + Derived(expression, "Derived", "Base", """
                override get [Symbol.toStringTag](): string { return "child"; }
                static override get [Symbol.toStringTag](): string { return "static"; }
                """) + "console.log(new Derived()[Symbol.toStringTag], Derived[Symbol.toStringTag]);";
        Assert.Equal("child static\n", TestHarness.Run(source, mode));
    }

    public static IEnumerable<object[]> InvalidCases()
    {
        foreach (bool expression in new[] { false, true })
        {
            foreach (string member in new[]
            {
                "override value() { return 1; }",
                "override get value() { return 1; }",
                "override set value(next: number) {}",
                "override get [Symbol.toStringTag](): string { return 'missing'; }",
                "static override get [Symbol.toStringTag](): string { return 'missing'; }"
            })
                foreach (var row in WithModes("class Base {}" +
                    Derived(expression, "Derived", "Base", member, generic: true), "TS4113"))
                    yield return row;

            foreach (string baseMembers in new[] { "", "#value() {}", "static #value() {}", "\"#value\"() {}", "static \"#value\"() {}" })
                foreach (string member in new[] { "override #value() {}", "static override #value() {}" })
                    foreach (var row in WithModes("class Base {" + baseMembers + "}" +
                        Derived(expression, "Derived", "Base", member), "TS4113"))
                        yield return row;

            foreach (bool getter in new[] { false, true })
            {
                string memberName = getter ? "get value()" : "value()";
                foreach (var row in WithModes(
                    "class Base { static " + memberName + ": number { return 1; } }" +
                    Derived(expression, "Derived", "Base", "static override " + memberName + " { return 'wrong'; }"), "TS2417"))
                    yield return row;

                // Fixed return types must remain literal, even though the child
                // member's fresh singleton return widens to number.
                foreach (string fixedReturn in new[]
                {
                    "return 1 as const;",
                    "const value: 1 = 1; return value;",
                    "const value = 1 as const; return value;",
                    "return fixedLiteral();"
                })
                    foreach (var row in WithModes(
                        "function fixedLiteral(): 1 { return 1; } class Base { static " + memberName + " { " + fixedReturn + " } }" +
                        Derived(expression, "Derived", "Base", "static override " + memberName + " { return 2; }"), "TS2417"))
                        yield return row;
            }

            foreach (var row in WithModes(
                "class Base { static value(flag: boolean) { if (flag) return 1; return 2; } }" +
                Derived(expression, "Derived", "Base", "static override value(flag: boolean) { return 3; }"), "TS2417"))
                yield return row;

            foreach (var row in WithModes(
                "class Base { static get [Symbol.toStringTag](): string { return 'base'; } }" +
                Derived(expression, "Derived", "Base", "static override get [Symbol.toStringTag]() { return 1; }"), "TS2417"))
                yield return row;
        }
    }

    [Theory, MemberData(nameof(InvalidCases))]
    public void InvalidOverridesKeepTheirTypeScriptDiagnostic(string source, string code, ExecutionMode mode)
    {
        var exception = Assert.IsAssignableFrom<TypeCheckException>(
            Assert.ThrowsAny<Exception>(() => TestHarness.Run(source, mode)));
        Assert.Equal(code, exception.Diagnostic.TsCode);
    }

    private static IEnumerable<object[]> WithModes(string source, string code)
    {
        foreach (ExecutionMode mode in new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            yield return [source, code, mode];
    }

    private static string Derived(bool expression, string name, string baseName, string members, bool generic = false)
    {
        string typeParameters = generic ? "<T>" : "";
        return expression
            ? $"const {name} = class{typeParameters} extends {baseName} {{ {members} }};"
            : $"class {name}{typeParameters} extends {baseName} {{ {members} }}";
    }
}
