using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DeletedNumberMetadataLookupTests
{
    public const string IssueSource = """
        const constructor: any = Number, prototype: any = Function.prototype;
        Object.defineProperty(prototype, "name", { value: "parent", configurable: true });
        delete constructor.name;
        console.log(constructor.name);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "parent\n"];
        yield return ["live-metadata-lifecycle", """
            const constructor: any = Number, prototype: any = Function.prototype;
            delete constructor.name; delete constructor.length;
            Object.defineProperty(prototype, "name", { value: "parent", writable: true, configurable: true });
            Object.defineProperty(prototype, "length", { value: 7, writable: true, configurable: true });
            console.log(constructor.name, constructor["length"],
                Object.hasOwn(constructor, "name"), Object.hasOwn(constructor, "length"));
            Object.defineProperty(constructor, "name", { value: "own", configurable: true });
            Object.defineProperty(constructor, "length", { value: 2, configurable: true });
            console.log(constructor.name, constructor.length,
                Object.hasOwn(constructor, "name"), Object.hasOwn(constructor, "length"));
            delete constructor["name"];
            Reflect.deleteProperty(constructor, "length");
            prototype.name = "changed"; prototype.length = 9;
            console.log(constructor["name"], constructor.length,
                Object.hasOwn(constructor, "name"), Object.hasOwn(constructor, "length"));
            delete prototype.name; delete prototype.length;
            console.log(typeof constructor.name, typeof constructor.length);
            """, "parent 7 false false\nown 2 true true\nchanged 9 false false\nundefined undefined\n"];
        yield return ["getter-receiver-and-own-accessor", """
            const constructor: any = Number, prototype: any = Function.prototype;
            const state = { inherited: 0, own: 0 }, receiver = { tag: "receiver", amount: 9 };
            delete constructor.name; delete constructor.length;
            Object.defineProperty(prototype, "name", {
                get: function(this: any) { state.inherited++; return this === constructor ? "native" : this.tag; },
                configurable: true
            });
            Object.defineProperty(prototype, "length", {
                get: function(this: any) { state.inherited++; return this === constructor ? 7 : this.amount; },
                configurable: true
            });
            console.log(constructor.name, constructor["name"], state.inherited);
            console.log(constructor.length, constructor["length"], state.inherited);
            const read: any = Reflect.get;
            console.log(read(constructor, "name", receiver), read(constructor, "length", receiver), state.inherited);
            Object.defineProperty(constructor, "name", { value: "own", configurable: true });
            Object.defineProperty(constructor, "length", { value: 2, configurable: true });
            console.log(constructor.name, constructor.length, read(constructor, "name", receiver), state.inherited);
            Object.defineProperty(constructor, "name", {
                get: function(this: any) { state.own++; return this === constructor ? "own-native" : this.tag; },
                configurable: true
            });
            console.log(constructor.name, read(constructor, "name", receiver), state.inherited, state.own);
            delete constructor.name;
            const marker = {};
            Object.defineProperty(prototype, "name", {
                get() { state.inherited++; throw marker; }, configurable: true
            });
            try { const value = constructor.name; }
            catch (error) { console.log(error === marker, state.inherited); }
            """, "native native 2\n7 7 4\nreceiver 9 6\nown 2 own 6\nown-native receiver 6 2\ntrue 7\n"];
        yield return ["own-nullish-and-getterless-precedence", """
            const constructor: any = Number, prototype: any = Function.prototype;
            const state = { reads: 0 };
            delete constructor.name; delete constructor.length;
            Object.defineProperty(prototype, "name", {
                get() { state.reads++; return "parent"; }, configurable: true
            });
            Object.defineProperty(prototype, "length", {
                get() { state.reads++; return 7; }, configurable: true
            });
            Object.defineProperty(constructor, "name", { value: undefined, configurable: true });
            Object.defineProperty(constructor, "length", { value: null, configurable: true });
            console.log(constructor.name === undefined, constructor.length === null,
                Object.hasOwn(constructor, "name"), Object.hasOwn(constructor, "length"), state.reads);
            Object.defineProperty(constructor, "name", { get: undefined, configurable: true });
            Object.defineProperty(constructor, "length", { set: undefined, configurable: true });
            console.log(constructor.name === undefined, constructor.length === undefined,
                Reflect.get(constructor, "name", {}) === undefined,
                Reflect.get(constructor, "length", {}) === undefined, state.reads);
            delete constructor.name; delete constructor.length;
            console.log(constructor.name, constructor.length, state.reads);
            """, "true true true true 0\ntrue true true true 0\nparent 7 2\n"];
        yield return ["custom-and-null-prototype", """
            const constructor: any = Number, first: any = { name: "custom", length: 5 };
            const state = { reads: 0 };
            Object.setPrototypeOf(constructor, first);
            delete constructor.name; delete constructor.length;
            console.log(constructor.name, constructor.length);
            first.name = "changed"; first.length = 6;
            console.log(constructor["name"], constructor["length"]);
            const second: any = {};
            Object.defineProperty(second, "name", {
                get: function(this: any) { state.reads++; return this === constructor ? "native" : "other"; },
                configurable: true
            });
            Object.setPrototypeOf(constructor, second);
            console.log(constructor.name, state.reads);
            Object.setPrototypeOf(constructor, null);
            console.log(constructor.name === undefined, constructor.length === undefined,
                Object.getPrototypeOf(constructor) === null, state.reads);
            """, "custom 5\nchanged 6\nnative 1\ntrue true true 1\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            // Interpreted native Number constructor prototype mutation remains unsupported.
            if ((string)row[0] != "custom-and-null-prototype")
                yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void DeletedNumberMetadataReadsTheLivePrototypeWithItsReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
