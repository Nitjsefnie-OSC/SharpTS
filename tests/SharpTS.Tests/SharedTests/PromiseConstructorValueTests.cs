using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PromiseConstructorValueTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["alias", """
            const P: any = Promise;
            const p: any = new P((resolve: any) => resolve(1));
            console.log(p === null);
            p.then((value: any) => console.log(value));
            new Promise(resolve => resolve(2)).then(value => console.log(value));
            """, "false\n1\n2\n"];
        yield return ["settlement", """
            const P: any = Promise;
            const token: any = {};
            const rejected: any = new P((resolve: any, reject: any) => {
                reject(token); resolve(9); throw "ignored";
            });
            const fulfilled: any = new P((resolve: any, reject: any) => {
                resolve(3); reject(token); throw "ignored";
            });
            const thrown: any = new P(() => { throw token; });
            console.log("constructed");
            rejected.catch((reason: any) => console.log("rejected", reason === token));
            fulfilled.then((value: any) => console.log("fulfilled", value));
            thrown.catch((reason: any) => console.log("thrown", reason === token));
            """, "constructed\nrejected true\nfulfilled 3\nthrown true\n"];
        yield return ["member-and-held", """
            let current: any = Promise;
            const held: any = current;
            const holder: any = { C: held };
            current = function () { return { replaced: true }; };
            const p: any = new held((resolve: any) => resolve(4));
            const q: any = new holder.C((resolve: any) => resolve(5));
            p.then((value: any) => console.log("held", value));
            q.then((value: any) => console.log("member", value));
            console.log(new current().replaced);
            """, "true\nheld 4\nmember 5\n"];
        yield return ["arguments", """
            const events: string[] = [];
            const holder: any = { C: Promise };
            function acquire(): any { events.push("callee"); return holder.C; }
            function note(label: string, value: any): any { events.push(label); return value; }
            const executor = (resolve: any) => { events.push("executor"); resolve(7); };
            const p: any = new (acquire())(
                note("first", executor), ...[note("spread", 99)], note("last", 100));
            events.push("after");
            console.log(events.join(","));
            events.length = 0;
            function replace(): any {
                events.push("replace");
                holder.C = function () { return {}; };
                return (resolve: any) => { events.push("member-executor"); resolve(10); };
            }
            const q: any = new holder.C(replace());
            console.log(events.join(","));
            p.then((value: any) => console.log(value));
            q.then((value: any) => console.log(value));
            """, "callee,first,spread,last,executor,after\nreplace,member-executor\n7\n10\n"];
        yield return ["argument-throw", """
            const P: any = Promise;
            const token: any = {};
            let calls = 0;
            function fail(): any { throw token; }
            try {
                new P((resolve: any) => { calls++; resolve(1); }, fail());
            } catch (error) { console.log(error === token, calls); }
            """, "true 0\n"];
        yield return ["bound", """
            const P: any = Promise;
            const Bound: any = P.bind(null);
            const p: any = new Bound((resolve: any) => resolve(8));
            const q: any = Reflect.construct(P, [(resolve: any) => resolve(9)]);
            p.then((value: any) => console.log("bound", value));
            q.then((value: any) => console.log("reflect", value));
            """, "bound 8\nreflect 9\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }.Select(mode =>
            new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ConstructorValuesUsePromiseExecutorSemantics(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
