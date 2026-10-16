using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ObjectFromEntriesTests
{
    [Theory, ModeData]
    public void EntryGetters_RunInOrderWithEntryReceiver(ExecutionMode mode)
    {
        const string source = """
            const entry: any[] = ["old", 1];
            let events = "";
            Object.defineProperty(entry, "0", {
                get() { events += "key:" + (this === entry) + ";"; return "key"; }
            });
            Object.defineProperty(entry, "1", {
                get() { events += "value:" + (this === entry) + ";"; return 42; }
            });
            const result = Object.fromEntries([entry]);
            console.log(result.key, events);
            """;
        Assert.Equal("42 key:true;value:true;\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void EntryProperties_UsePrototypeGettersAndDoNotReadLength(ExecutionMode mode)
    {
        const string source = """
            const entry: any[] = [];
            const prototype: any = {};
            let events = "";
            Object.defineProperty(prototype, "0", {
                get() { events += "key:" + (this === entry) + ";"; return "inherited"; }
            });
            Object.defineProperty(prototype, "1", {
                get() { events += "value:" + (this === entry) + ";"; return 7; }
            });
            Object.setPrototypeOf(entry, prototype);
            const result = Object.fromEntries([entry]);
            console.log(result.inherited, events, entry.length);
            const record: any = { 0: "record", 1: 9 };
            Object.defineProperty(record, "length", { get() { throw "length read"; } });
            console.log(Object.fromEntries([record]).record);
            console.log(Object.fromEntries([["missing"]]).missing === undefined);
            """;
        Assert.Equal("7 key:true;value:true; 0\n9\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void InheritedArrayIndexGetters_KeepEntryAsReceiver(ExecutionMode mode)
    {
        const string source = """
            const prototype: any[] = ["key", 1];
            const entry: any[] = [];
            Object.defineProperty(prototype, "0", {
                get() { console.log(this === entry); return "key"; }
            });
            Object.defineProperty(prototype, "1", {
                get() { console.log(this === entry); return 1; }
            });
            Object.setPrototypeOf(entry, prototype);
            console.log(Object.fromEntries([entry]).key);
            """;
        Assert.Equal("true\ntrue\n1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void KeyGetterFailure_PreservesThrownValueAndSkipsValueGetter(ExecutionMode mode)
    {
        const string source = """
            const entry: any[] = ["key", 1];
            const original = {};
            let reads = "";
            Object.defineProperty(entry, "0", {
                get() { reads += "key"; throw original; }
            });
            Object.defineProperty(entry, "1", {
                get() { reads += "value"; return 2; }
            });
            try { Object.fromEntries([entry]); console.log("returned"); }
            catch (error) { console.log(error === original, reads); }
            """;
        Assert.Equal("true key\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ValueGetterFailure_PreservesThrownValue(ExecutionMode mode)
    {
        const string source = """
            const entry: any[] = ["key", 1];
            const original = {};
            let reads = "";
            Object.defineProperty(entry, "0", {
                get() { reads += "key;"; return "key"; }
            });
            Object.defineProperty(entry, "1", {
                get() { reads += "value;"; throw original; }
            });
            try { Object.fromEntries([entry]); console.log("returned"); }
            catch (error) { console.log(error === original, reads); }
            """;
        Assert.Equal("true key;value;\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void PrimitiveEntries_ThrowTypeError(ExecutionMode mode)
    {
        const string source = """
            const entries: any[] = [null, undefined, true, 1, "ab", 1n, Symbol("entry")];
            for (const entry of entries) {
                try { Object.fromEntries([entry]); console.log("returned"); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            """;
        Assert.Equal("true\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\n", TestHarness.Run(source, mode));
    }

    // Compiled consumers materialize the source before processing entries (#2123).
    [Theory, InterpretedOnlyData]
    public void EntryGetterFailure_ClosesIteratorAndPreservesOriginalThrow(ExecutionMode mode)
    {
        const string source = """
            const entry: any[] = ["key", 1];
            const original = {};
            let events = "";
            Object.defineProperty(entry, "0", {
                get() { events += "get;"; throw original; }
            });
            function* entries() {
                try { yield entry; events += "resumed;"; }
                finally { events += "close;"; throw "cleanup"; }
            }
            try { Object.fromEntries(entries()); }
            catch (error) { console.log(error === original, events); }
            """;
        Assert.Equal("true get;close;\n", TestHarness.Run(source, mode));
    }
}
