using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DotNetImportedTypeValueTests
{
    public const string IssueSource = """
        import { Version } from "dotnet:System.Version";
        console.log(new Version(1, 2).toString());
        const constructor: any = Version;
        console.log(typeof constructor);
        """;

    public const string DecoratedSource = """
        @DotNetType("System.Version")
        declare class ImportedVersion {
            constructor(major: number, minor: number);
            toString(): string;
        }
        const value: any = ImportedVersion;
        function read() { return ImportedVersion; }
        console.log(new ImportedVersion(1, 2).toString(), typeof ImportedVersion);
        console.log(typeof value, value === ImportedVersion, read() === value);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "1.2\nfunction\n"];
        yield return ["alias-and-direct-typeof", """
            import { Version as Release } from "dotnet:System.Version";
            const first: any = Release;
            function read() { console.log(typeof Release); return Release; }
            console.log(typeof Release, typeof first, first === Release);
            console.log(read() === first, Release === first, new Release(1, 2).toString());
            """, "function function true\nfunction\ntrue true 1.2\n"];
        yield return ["lexical-shadows", """
            import { Version } from "dotnet:System.Version";
            const original: any = Version;
            function parameter(Version: any) { const read = () => Version; return read(); }
            function local() { const Version = "local"; return Version; }
            { const Version: any = "block"; console.log(Version, typeof Version); }
            console.log(parameter("parameter"), local(), Version === original, typeof Version);
            """, "block string\nparameter local true function\n"];
        yield return ["mutable-local-constructor-capture", """
            import { Version, Version as ImportedVersion } from "dotnet:System.Version";
            let outside: typeof ImportedVersion = ImportedVersion;
            function run() {
                let Version: typeof ImportedVersion = ImportedVersion;
                const state: any = { release: undefined };
                const gate = new Promise<void>(resolve => { state.release = resolve; });
                const read = async (): Promise<void> => {
                    await gate;
                    console.log(Version === null, outside === null, typeof Version, typeof outside);
                };
                const work = read();
                Version = null as any;
                outside = null as any;
                state.release();
                return work;
            }
            run().then(() => console.log("done"), (error: any) => console.log("failed", String(error)));
            """, "true true object object\ndone\n"];
        yield return ["async-function", """
            import { Version } from "dotnet:System.Version";
            const state: any = { release: undefined };
            const gate = new Promise<void>(resolve => { state.release = resolve; });
            async function read(): Promise<any> {
                await gate;
                const value: any = Version;
                console.log(typeof Version, typeof value, value === Version);
                return value;
            }
            const work = read();
            console.log("pending");
            state.release();
            work.then((value: any) => console.log("done", value === Version),
                (error: any) => console.log("failed", String(error)));
            """, "pending\nfunction function true\ndone true\n"];
        yield return ["async-arrow", """
            import { Version } from "dotnet:System.Version";
            const state: any = { release: undefined };
            const gate = new Promise<void>(resolve => { state.release = resolve; });
            const read = async (): Promise<any> => {
                await gate;
                const value: any = Version;
                console.log(typeof Version, typeof value, value === Version);
                return value;
            };
            const work = read();
            console.log("pending");
            state.release();
            work.then((value: any) => console.log("done", value === Version),
                (error: any) => console.log("failed", String(error)));
            """, "pending\nfunction function true\ndone true\n"];
        yield return ["generator", """
            import { Version } from "dotnet:System.Version";
            function* read() {
                yield "ready";
                console.log(typeof Version);
                yield Version;
                return Version;
            }
            const iterator = read();
            console.log(iterator.next().value);
            const yielded: any = iterator.next(), returned: any = iterator.next();
            console.log(typeof yielded.value, yielded.value === Version,
                returned.value === Version, returned.done);
            """, "ready\nfunction\nfunction true true true\n"];
        yield return ["guest-class-collision", """
            import { Version as CLRVersion } from "dotnet:System.Version";
            class Version { tag = "guest"; }
            const guest: any = Version, imported: any = CLRVersion;
            console.log(new Version().tag, new CLRVersion(1, 2).toString());
            console.log(guest === Version, imported === CLRVersion, guest !== imported,
                typeof Version, typeof CLRVersion);
            """, "guest 1.2\ntrue true true function function\n"];
        yield return ["global-aliases", """
            import { Version as NaN } from "dotnet:System.Version";
            import { StringBuilder as Number } from "dotnet:System.Text.StringBuilder";
            const first: any = NaN, second: any = Number;
            console.log(typeof NaN, typeof Number, typeof first, typeof second);
            async function read() {
                await Promise.resolve();
                console.log(typeof NaN, typeof Number, NaN === first, Number === second);
            }
            const arrow = async () => {
                await Promise.resolve();
                console.log(typeof NaN, typeof Number, NaN === first, Number === second);
            };
            read().then(() => arrow());
            """, "function function function function\nfunction function true true\nfunction function true true\n"];
    }

    public static Dictionary<string, string> ModuleFiles(bool reverseOrder)
    {
        var imports = reverseOrder
            ? "import { readBuilder, builderValue } from './builder';\nimport { readVersion, versionValue, readVersionAsync } from './version';"
            : "import { readVersion, versionValue, readVersionAsync } from './version';\nimport { readBuilder, builderValue } from './builder';";
        return new Dictionary<string, string>
        {
            ["./version.ts"] = """
                import { Version as Host } from "dotnet:System.Version";
                export const versionValue: any = Host;
                export function readVersion(): any { return Host; }
                export const readVersionAsync = async (gate: Promise<void>): Promise<any> => {
                    await gate;
                    console.log(typeof Host);
                    return Host;
                };
                """,
            ["./builder.ts"] = """
                import { StringBuilder as Host } from "dotnet:System.Text.StringBuilder";
                export const builderValue: any = Host;
                export function readBuilder(): any { return Host; }
                """,
            ["./function.ts"] = """
                export function Host() { return "guest-function"; }
                """,
            ["./main.ts"] = imports + "\n" + """
                import { Version as CLRVersion } from "dotnet:System.Version";
                import { StringBuilder as CLRBuilder } from "dotnet:System.Text.StringBuilder";
                import { Host as guestFunction } from "./function";
                class Host { tag = "guest"; }
                const guest: any = Host;
                function readGuest() { return Host; }
                console.log(readVersion() === CLRVersion, versionValue === CLRVersion,
                    readBuilder() === CLRBuilder, builderValue === CLRBuilder,
                    readVersion() !== readBuilder(), readGuest() === guest);
                console.log(typeof readVersion(), typeof readBuilder(), new Host().tag,
                    guest !== CLRVersion, guest !== CLRBuilder);
                const state: any = { release: undefined };
                const gate = new Promise<void>(resolve => { state.release = resolve; });
                readVersionAsync(gate).then((value: any) => console.log(value === CLRVersion, guestFunction()),
                    (error: any) => console.log("failed", String(error)));
                state.release();
                """
        };
    }

    public const string ModuleExpected = "true true true true true true\nfunction function guest true true\nfunction\ntrue guest-function\n";

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ImportedClrTypesRemainValuesInCheckedModules(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(
            new Dictionary<string, string> { ["./main.ts"] = source }, "./main.ts", mode));
    }

    [Theory]
    [InlineData(false, ExecutionMode.Interpreted)]
    [InlineData(false, ExecutionMode.Compiled)]
    [InlineData(true, ExecutionMode.Interpreted)]
    [InlineData(true, ExecutionMode.Compiled)]
    public void ModuleAliasesAndForeignGuestNamesRetainTheirOwnIdentity(bool reverseOrder, ExecutionMode mode)
        => Assert.Equal(ModuleExpected, TestHarness.RunModules(ModuleFiles(reverseOrder), "./main.ts", mode));

    [Theory, ModeData]
    public void DecoratedExternalDeclarationRemainsAValue(ExecutionMode mode)
        => Assert.Equal("1.2 function\nfunction true true\n",
            TestHarness.Run(DecoratedSource, mode, DecoratorMode.Legacy));
}
