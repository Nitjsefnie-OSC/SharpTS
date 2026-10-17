using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ObjectMethodReadIdentityTests
{
    public const string Source = """
        const object: any = { method() { return this; } };
        console.log(object.method === object.method);
        const extracted: any = object.method;
        console.log(extracted() === globalThis);
        const copied: any = { method: extracted };
        console.log(copied.method() === copied, extracted.call(copied) === copied);
        """;

    public const string Expected = "true\ntrue\ntrue true\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", Source, Expected];
        yield return ["data-read-forms", """
            const key = Symbol("method");
            function declared(this: any) { return this; }
            const expression: any = function(this: any) { return this; };
            const generator: any = function*() { yield 1; };
            const prototype: any = { method() { return this; } };
            const object: any = Object.create(prototype);
            object.expression = expression;
            object.declared = declared;
            object[1] = expression;
            object[key] = expression;
            object.generator = generator;
            const descriptorValue: any = function(this: any) { return this; };
            Object.defineProperty(object, "data", {
                value: descriptorValue, writable: true, enumerable: true, configurable: true
            });
            const descriptor: any = Object.getOwnPropertyDescriptor(object, "data");
            console.log(prototype.method === prototype.method, object.method === prototype.method,
                object.method === object["method"]);
            console.log(object.expression === expression, object.expression === object["expression"],
                object.declared === declared, object[1] === expression, object[key] === expression,
                object.generator === generator);
            console.log(object.data === descriptor.value, object.data === descriptorValue);
            console.log(object.method() === object, object.expression() === object,
                object.declared() === object, object[1]() === object, object[key]() === object,
                object.data() === object);
            """, "true true true\ntrue true true true true true\ntrue true\ntrue true true true true true\n"];
        yield return ["iterator-return-receiver", """
            let calls = 0;
            const iterator: any = {
                next() { return { value: 1, done: false }; },
                *return(this: any, value: any = (calls++, console.log(this === iterator))) {}
            };
            const iterable: any = { [Symbol.iterator]() { return iterator; } };
            for (const value of iterable) { break; }
            console.log(calls);
            """, "true\n1\n"];
        yield return ["thenable-receiver", """
            const thenable: any = {
                value: 7,
                then(resolve: any) {
                    console.log(this === thenable);
                    resolve(this.value);
                }
            };
            async function run() {
                console.log(await thenable);
                console.log(await Promise.resolve(thenable));
            }
            run();
            """, "true\n7\ntrue\n7\n"];
        yield return ["own-this-and-copy", """
            function make(this: any) {
                return {
                    sloppy() { return this; },
                    strict() { "use strict"; return this; },
                    expression: function(this: any) { return this; },
                    strictExpression: function(this: any) { "use strict"; return this; },
                    defaultThis: function(this: any, receiver: any = this) { return receiver; },
                    arrow: () => this
                };
            }
            const outer: any = {}, object: any = make.call(outer);
            const sloppy: any = object.sloppy, strict: any = object.strict;
            const expression: any = object.expression, strictExpression: any = object.strictExpression;
            console.log(object.sloppy === sloppy, sloppy() === globalThis, strict() === undefined,
                expression() === globalThis, strictExpression() === undefined);
            const copied: any = { method: sloppy, expression, arrow: object.arrow };
            console.log(copied.method === sloppy, copied.method() === copied,
                sloppy.call(copied) === copied, sloppy.apply(copied, []) === copied,
                copied.expression() === copied);
            const bound: any = sloppy.bind(outer), holder: any = { method: bound };
            console.log(holder.method === bound, holder.method() === outer, bound.call(copied) === outer);
            console.log(copied.arrow === object.arrow, copied.arrow() === outer,
                copied.arrow.call(copied) === outer);
            const defaultThis: any = object.defaultThis;
            console.log(defaultThis() === globalThis, object.defaultThis() === object,
                defaultThis.call(copied) === copied);
            """, "true true true true true\ntrue true true true true\ntrue true true\ntrue true true\ntrue true true\n"];
        yield return ["call-reference-forms", """
            const object: any = { method() { "use strict"; return this; } };
            console.log((object.method)() === object, (object["method"])() === object,
                (object.method as any)() === object);
            const maybe: any = object;
            console.log(maybe?.method() === object, maybe.method?.() === object,
                (maybe.method)?.() === object, maybe?.["method"]?.() === object);
            function discard() {}
            console.log((discard(), object.method)() === undefined,
                (true ? object.method : object.method)() === undefined);
            const absent: any = null;
            let effects = 0;
            function sideEffect() { effects++; return 1; }
            console.log(absent?.method(sideEffect()) === undefined, effects);
            """, "true true true\ntrue true true true\ntrue true\ntrue 0\n"];
        yield return ["grouped-optional-arguments", """
            const absent: any = null;
            let effects = 0;
            function sideEffect() { effects++; return 1; }
            try { (absent?.method)(sideEffect()); }
            catch (error) { console.log(error instanceof TypeError, effects); }
            """, "true 1\n"];
        yield return ["non-null-reference", """
            const object: any = { method() { "use strict"; return this; } };
            console.log(object.method!() === object);
            """, "true\n"];
        yield return ["getter-and-tags", """
            let reads = 0, receiverReads = 0, keyReads = 0, substitutions = 0;
            const events: string[] = [];
            const method: any = function(this: any) { "use strict"; return this; };
            const tag: any = function(this: any, strings: any, value: any) {
                "use strict";
                events.push("call");
                return (this === object) + ":" + value;
            };
            const object: any = {
                get method() { reads++; return method; },
                get arrow() { return () => this; },
                get tag() { reads++; events.push("get"); return tag; }
            };
            console.log(object.method === method, object.method === method, reads);
            const extracted: any = object.method;
            console.log(extracted() === undefined, object.method() === object, reads);
            const arrow: any = object.arrow;
            console.log(arrow() === object, arrow.call({}) === object);
            function receiver() { receiverReads++; events.push("receiver"); return object; }
            function key() { keyReads++; events.push("key"); return "tag"; }
            function value() { substitutions++; events.push("value"); return 7; }
            reads = 0;
            console.log(receiver()[key()]`value:${value()}`,
                receiverReads, keyReads, reads, substitutions, events.join(","));
            const detached: any = object.tag;
            events.length = 0;
            console.log(detached`value:${9}`, events.join(","));
            """, "true true 2\ntrue true 4\ntrue true\ntrue:7 1 1 1 1 receiver,key,get,value,call\nfalse:9 call\n"];
        yield return ["async-member-tags", """
            let reads = 0, receiverReads = 0, keyReads = 0, substitutions = 0;
            const events: string[] = [];
            const tag: any = function(this: any, strings: any, value: any) {
                "use strict";
                events.push("call");
                return (this === object) + ":" + value;
            };
            const object: any = { get tag() { reads++; events.push("get"); return tag; } };
            function receiver() { receiverReads++; events.push("receiver"); return object; }
            function key() { keyReads++; events.push("key"); return "tag"; }
            function value() { substitutions++; events.push("value"); return 7; }
            async function run() {
                const immediate = receiver()[key()]`value:${value()}`;
                console.log(immediate, receiverReads, keyReads, reads, substitutions, events.join(","));
                reads = 0; receiverReads = 0; keyReads = 0; substitutions = 0; events.length = 0;
                const result = receiver()[key()]`value:${await Promise.resolve(value())}`;
                console.log(result, receiverReads, keyReads, reads, substitutions, events.join(","));
            }
            run();
            """, "true:7 1 1 1 1 receiver,key,get,value,call\ntrue:7 1 1 1 1 receiver,key,get,value,call\n"];
        yield return ["constructor-values", """
            let observed: any;
            const Constructor: any = function(this: any) {
                observed = new.target;
                this.value = 7;
                return this;
            };
            const holder: any = { Constructor };
            console.log(holder.Constructor === Constructor, holder.Constructor === holder.Constructor);
            console.log(holder.Constructor() === holder);
            const instance = new holder.Constructor();
            console.log(observed === Constructor, instance.value, instance !== holder);
            const extracted: any = holder.Constructor, receiver: any = {};
            console.log(extracted.call(receiver) === receiver);
            const Class: any = class Named {
                target: any;
                constructor() { this.target = new.target; }
            };
            holder.ClassValue = Class;
            console.log(holder.ClassValue === Class, holder.ClassValue === holder.ClassValue);
            const child = new holder.ClassValue();
            console.log(child.target === Class, Object.getPrototypeOf(child) === Class.prototype);
            try { holder.ClassValue(); }
            catch (error) { console.log(error instanceof TypeError); }
            const inherited: any = Object.create(holder);
            console.log(inherited.ClassValue === Class, inherited.Constructor === Constructor);
            """, "true true\ntrue\ntrue 7 true\ntrue\ntrue true\ntrue true\ntrue\ntrue true\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        // These compiler receiver boundaries are tracked independently; every
        // source still runs through interpreted, resolved and CLI coverage.
        // #2210: non-null member calls.
        // #2250: arguments of calls to a grouped optional member.
        => Cases().SelectMany(row => ((string)row[0] is "non-null-reference" or "grouped-optional-arguments"
                ? new[] { ExecutionMode.Interpreted }
                : new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled })
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void DataReadsPreserveIdentityAndCallsSupplyTheirReceiver(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
