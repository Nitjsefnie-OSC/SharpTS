using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class AsyncLoopBodyCellCaptureTests
{
    private const string Exact = """
        for (const seed of [1]) {
            let value = 0;
            const unused = async () => value;
            const object: any = {};
            Object.defineProperty(object, "x", { get: () => ++value });
            console.log(typeof unused, object.x, value);
        }
        """;

    private const string Ownership = """
        function make(seed: number) {
            const records: any[] = [];
            for (const step of [1, 2]) {
                let value = seed + step;
                const read = async (wait: Promise<any>, offset: number) => {
                    await wait;
                    return [seed, value, offset];
                };
                records.push({ read, bump: () => ++value, write: (next: number) => { value = next; } });
            }
            return records;
        }
        async function run() {
            const one = make(0), two = make(10);
            let release: any;
            const gate = new Promise<any>(resolve => { release = resolve; });
            const pending = one[0].read(gate, 4);
            console.log(one[0].bump(), one[1].bump(), two[0].bump());
            one[0].write(7);
            release(0);
            console.log((await pending).join(","));
            console.log((await one[1].read(Promise.resolve(0), 5)).join(","),
                (await two[0].read(Promise.resolve(0), 6)).join(","));
        }
        run().catch((error: any) => console.log("unexpected", error.name, error.message));
        """;

    private const string PendingWriter = """
        function make() {
            const records: any[] = [];
            for (const seed of [1]) {
                let value = seed;
                const read = () => value;
                const write = async (wait: Promise<any>) => {
                    value = 2;
                    value += 3;
                    await wait;
                    value = 7;
                    value += 4;
                    return value;
                };
                let release: any;
                const gate = new Promise<any>(resolve => { release = resolve; });
                const pending = write(gate);
                console.log("owner", value, read());
                records.push({ read, pending, release });
            }
            return records[0];
        }
        async function run() {
            const record = make();
            console.log("sibling", record.read());
            record.release(0);
            console.log("done", await record.pending, record.read());
            const noncapturing = async () => 99;
            console.log("noncapture", await noncapturing());
        }
        run();
        """;

    private const string NestedRelay = """
        function make() {
            const records: any[] = [];
            for (const seed of [1]) {
                let value = seed;
                const read = () => value;
                const relay = async (wait: Promise<any>) => {
                    const sync = (amount: number) => { value += amount; return value; };
                    const nested = async () => {
                        value = 3;
                        value += 2;
                        await wait;
                        value += 4;
                        return sync(1);
                    };
                    const pending = nested();
                    console.log("relay", value, sync(1));
                    return await pending;
                };
                records.push({ read, relay });
            }
            return records[0];
        }
        async function run() {
            const record = make();
            let release: any;
            const gate = new Promise<any>(resolve => { release = resolve; });
            const pending = record.relay(gate);
            console.log("outside", record.read());
            release(0);
            console.log("done", await pending, record.read());
        }
        run();
        """;

    private const string OwnBindingShadows = """
        function make() {
            const records: any[] = [];
            for (const seed of [1]) {
                let value = seed;
                const read = () => value;
                const bump = () => ++value;
                const capture = async (wait: Promise<any>) => {
                    {
                        let value = 50;
                        const bumpShadow = (amount: number) => { value += amount; return value; };
                        bumpShadow(1);
                        await wait;
                        bumpShadow(2);
                        if (value !== 53) throw new Error("incoming-cell block shadow");
                    }
                    return value;
                };
                const shadow = async (value: number, wait: Promise<any>) => {
                    console.log("parameter", value);
                    {
                        let value = 50;
                        value += 1;
                        console.log("block-before", value);
                        await wait;
                        value += 2;
                        console.log("block-after", value);
                    }
                    value += 3;
                    return value;
                };
                records.push({ read, bump, capture, shadow });
            }
            return records[0];
        }
        async function run() {
            const record = make();
            let release: any;
            const gate = new Promise<any>(resolve => { release = resolve; });
            const captured = record.capture(gate);
            const shadowed = record.shadow(10, gate);
            console.log("outer", record.bump(), record.read());
            release(0);
            console.log("results", await shadowed, await captured, record.read());
        }
        run();
        """;

    private const string HeaderAndBody = """
        function make() {
            const records: any[] = [];
            for (let index = 0; index < 2; index++) {
                let value = index * 10;
                const body = async (wait: Promise<any>) => { await wait; return value; };
                records.push({ header: () => index, body, bump: () => ++value });
            }
            return records;
        }
        async function run() {
            const records = make();
            let release: any;
            const gate = new Promise<any>(resolve => { release = resolve; });
            const first = records[0].body(gate), second = records[1].body(gate);
            console.log("headers", records[0].header(), records[1].header());
            console.log("bodies", records[0].bump(), records[1].bump());
            release(0);
            console.log("pending", await first, await second);
            console.log("headers", records[0].header(), records[1].header());
        }
        run();
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return Case("exact-uninvoked-async-getter", Exact, "function 1 1\n");
        yield return Case("factory-and-activation-ownership", Ownership, "2 3 12\n0,7,4\n0,3,5 10,12,6\n");
        yield return Case("plain-compound-pending-writer", PendingWriter, "owner 5 5\nsibling 5\ndone 11 11\nnoncapture 99\n");
        yield return Case("nested-async-writer-and-sync-child", NestedRelay, "relay 5 6\noutside 6\ndone 11 11\n");
        yield return Case("own-parameter-and-block-shadow", OwnBindingShadows,
            "parameter 10\nblock-before 51\nouter 2 2\nblock-after 53\nresults 13 2 2\n");
        yield return Case("synchronous-header-and-async-body", HeaderAndBody,
            "headers 0 1\nbodies 1 11\npending 1 11\nheaders 0 1\n");
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void AsyncDescendantsShareTheOwningSynchronousLoopBodyCell(
        string name, Dictionary<string, string> files, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.RunModules(files, "main.ts", mode));
    }

    private static object[] Case(string name, string source, string expected) =>
        [name, new Dictionary<string, string> { ["main.ts"] = source }, expected];
}
