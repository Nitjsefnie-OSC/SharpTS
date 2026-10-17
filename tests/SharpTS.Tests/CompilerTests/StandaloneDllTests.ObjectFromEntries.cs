using Xunit;

namespace SharpTS.Tests.CompilerTests;

public partial class StandaloneDllTests
{
    [Fact]
    public void Isolated_Issue2121ObjectFromEntries_UsesOrdinaryEntryGets()
    {
        const string source = """
            const entry: any[] = ["old", 1];
            let events = "";
            Object.defineProperty(entry, "0", {
                get() { events += "key;"; return "key"; }, configurable: true
            });
            Object.defineProperty(entry, "1", {
                get() { events += "value;"; return 42; }
            });
            console.log(Object.fromEntries([entry]).key, events);
            const original = {};
            Object.defineProperty(entry, "0", { get() { throw original; } });
            try { Object.fromEntries([entry]); console.log("returned"); }
            catch (error) { console.log(error === original); }
            const inherited: any[] = [];
            const prototype: any = { 1: 7 };
            Object.defineProperty(prototype, "0", {
                get() { console.log(this === inherited); return "inherited"; }
            });
            Object.setPrototypeOf(inherited, prototype);
            console.log(Object.fromEntries([inherited]).inherited);
            """;
        using var directory = IntegrationTests.CliTestHelper.CreateTempDirectory();
        var path = directory.CreateFile("main.ts", source);
        AssertStandaloneConstructorIdentity(directory.Path, path,
            directory.GetPath("from-entries.dll"), "42 key;value;\ntrue\ntrue\n7\n");
    }
}
