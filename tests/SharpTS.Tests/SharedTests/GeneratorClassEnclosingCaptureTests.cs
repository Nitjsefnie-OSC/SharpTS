using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class GeneratorClassEnclosingCaptureTests
{
    public const string Source = """
        function make(value: number) {
            return class { *values() { yield () => value; } };
        }
        const First = make(7), Second = make(9);
        const one: any = new First().values().next().value;
        const two: any = new Second().values().next().value;
        console.log(one(), two());
        """;

    public const string Expected = "7 9\n";

    // The integration tests run every row with VariableResolver and through the
    // CLI. Only the original repro pins compiled behavior here: other local
    // class shapes have independent compiler capture/identity limitations.
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", Source, Expected];
        yield return ["direct-and-async", """
            function make(value: number) {
                return class {
                    *values() { yield value; yield () => () => value; }
                    async *asyncValues() {
                        await Promise.resolve(0);
                        yield value;
                        yield () => value;
                    }
                };
            }
            async function run() {
                const First = make(13), Second = make(17);
                const first = new First(), second = new Second();
                const one = first.values(), two = second.values();
                console.log(one.next().value, two.next().value);
                const readOne: any = one.next().value, readTwo: any = two.next().value;
                console.log(readOne()(), readTwo()());
                const asyncOne = first.asyncValues(), asyncTwo = second.asyncValues();
                console.log((await asyncOne.next()).value, (await asyncTwo.next()).value);
                const laterOne: any = (await asyncOne.next()).value;
                const laterTwo: any = (await asyncTwo.next()).value;
                console.log(laterOne(), laterTwo());
            }
            run();
            """, "13 17\n13 17\n13 17\n13 17\n"];
        yield return ["named-derived-and-declaration", """
            class Parent { read() { return "parent"; } }
            function makeNamed(value: number) {
                return class Named extends Parent {
                    *values() { yield value; yield () => Named; yield () => super.read(); }
                    async *asyncValues() { await Promise.resolve(0); yield () => value; }
                };
            }
            function makeDeclared(value: number) {
                class Local {
                    *values() { yield () => value; yield () => Local; }
                }
                return Local;
            }
            async function run() {
                const First = makeNamed(19), Second = makeNamed(23);
                const one = new First().values(), two = new Second().values();
                console.log(one.next().value, two.next().value);
                const selfOne: any = one.next().value, selfTwo: any = two.next().value;
                console.log(selfOne() === First, selfTwo() === Second);
                const parentOne: any = one.next().value, parentTwo: any = two.next().value;
                console.log(parentOne(), parentTwo());
                const laterOne: any = (await new First().asyncValues().next()).value;
                const laterTwo: any = (await new Second().asyncValues().next()).value;
                console.log(laterOne(), laterTwo());
                const LocalOne = makeDeclared(29), LocalTwo = makeDeclared(31);
                const localOne = new LocalOne().values(), localTwo = new LocalTwo().values();
                const valueOne: any = localOne.next().value, valueTwo: any = localTwo.next().value;
                const classOne: any = localOne.next().value, classTwo: any = localTwo.next().value;
                console.log(valueOne(), classOne() === LocalOne, valueTwo(), classTwo() === LocalTwo);
            }
            run();
            """, "19 23\ntrue true\nparent parent\n19 23\n29 true 31 true\n"];
        yield return ["live-cells-and-interleaving", """
            function make(seed: number) {
                let value = seed;
                return {
                    Class: class {
                        *values() {
                            yield () => value;
                            value += 1;
                            yield () => value;
                            value += 10;
                            yield value;
                        }
                        async *asyncValues() {
                            yield () => value;
                            await Promise.resolve(0);
                            value += 2;
                            yield () => value;
                        }
                    },
                    set(next: number) { value = next; },
                    get() { return value; }
                };
            }
            async function run() {
                const first = make(1), second = make(10);
                const one = new first.Class().values(), two = new second.Class().values();
                const readOne: any = one.next().value, readTwo: any = two.next().value;
                console.log(readOne(), readTwo());
                first.set(5); second.set(20);
                console.log(readOne(), readTwo());
                const nextOne: any = one.next().value, nextTwo: any = two.next().value;
                console.log(nextOne(), readOne(), nextTwo(), readTwo());
                console.log(one.next().value, two.next().value, first.get(), second.get());
                const asyncOne = new first.Class().asyncValues(), asyncTwo = new second.Class().asyncValues();
                const oldOne: any = (await asyncOne.next()).value;
                const oldTwo: any = (await asyncTwo.next()).value;
                first.set(100); second.set(200);
                const newOne: any = (await asyncOne.next()).value;
                const newTwo: any = (await asyncTwo.next()).value;
                console.log(oldOne(), newOne(), oldTwo(), newTwo());
            }
            run();
            """, "1 10\n5 20\n6 6 21 21\n16 31 16 31\n102 102 202 202\n"];
        yield return ["defaults-locals-arguments-and-receiver", """
            function make(value: number) {
                return class {
                    tag = "instance";
                    *values(next: number = value, read: any = () => value) {
                        let local = next + 1;
                        yield () => [value, next, local, read(), arguments.length, this.tag].join("|");
                        local += 10;
                        yield () => [value, next, local, read(), typeof arguments[0], this.tag].join("|");
                    }
                    async *asyncValues(next: number = value, read: any = () => value) {
                        let local = next + 1;
                        await Promise.resolve(0);
                        yield () => [value, next, local, read(), arguments.length, this.tag].join("|");
                    }
                };
            }
            async function run() {
                const Class = make(37), instance = new Class();
                const defaults = instance.values();
                const first: any = defaults.next().value;
                console.log(first());
                const second: any = defaults.next().value;
                console.log(first(), second());
                instance.tag = "changed";
                const explicit: any = instance.values(41, () => 43).next().value;
                console.log(explicit());
                const later: any = (await instance.asyncValues(undefined).next()).value;
                console.log(later());
            }
            run();
            """, "37|37|38|37|0|instance\n37|37|48|37|0|instance 37|37|48|37|undefined|instance\n37|41|42|43|2|changed\n37|37|38|37|1|changed\n"];
        yield return ["private-symbol-and-static", """
            const key = Symbol("values");
            function make(value: number) {
                return class {
                    #secret = value + 1;
                    *#privateValues() { yield () => value + this.#secret; }
                    async *#privateAsyncValues() {
                        await Promise.resolve(0);
                        yield () => value + this.#secret;
                    }
                    privateValues() { return this.#privateValues(); }
                    privateAsyncValues() { return this.#privateAsyncValues(); }
                    *[key]() { yield () => [value, this.#secret].join(":"); }
                    static tag = "static";
                    static *values() { yield () => [value, this.tag].join(":"); }
                    static async *asyncValues() {
                        await Promise.resolve(0);
                        yield () => [value, this.tag].join(":");
                    }
                };
            }
            async function run() {
                const Class = make(43), instance = new Class();
                const privateRead: any = instance.privateValues().next().value;
                const symbolRead: any = instance[key]().next().value;
                const staticRead: any = Class.values().next().value;
                console.log(privateRead(), symbolRead(), staticRead());
                const privateLater: any = (await instance.privateAsyncValues().next()).value;
                const staticLater: any = (await Class.asyncValues().next()).value;
                console.log(privateLater(), staticLater());
            }
            run();
            """, "87 43:44 43:static\n87 43:static\n"];
        yield return ["receiver-views", """
            function make(value: number) {
                return class {
                    tag = "instance";
                    *values() { yield () => [value, this === undefined ? "undefined" : this.tag].join(":"); }
                    async *asyncValues() {
                        await Promise.resolve(0);
                        yield () => [value, this === undefined ? "undefined" : this.tag].join(":");
                    }
                };
            }
            async function run() {
                const Class = make.call({ tag: "outer" }, 59);
                class Child extends Class {}
                const child = new Child(); child.tag = "child";
                const inherited: any = child.values().next().value;
                const method: any = Class.prototype.values, asyncMethod: any = Class.prototype.asyncValues;
                const borrowed: any = method.call({ tag: "borrowed" }).next().value;
                const rebound: any = method.bind({ tag: "bound" }).call({ tag: "ignored" }).next().value;
                const detached: any = method().next().value;
                console.log(inherited(), borrowed(), rebound(), detached());
                const asyncBorrowed: any = (await asyncMethod.call({ tag: "async" }).next()).value;
                const asyncDetached: any = (await asyncMethod().next()).value;
                console.log(asyncBorrowed(), asyncDetached());
            }
            run();
            """, "59:child 59:borrowed 59:bound 59:undefined\n59:async 59:undefined\n"];
        yield return ["nonclass-generator-wrappers", """
            function make(value: number) {
                function* declaration() { yield () => value; }
                const expression: any = function*(this: any, next: number) {
                    yield () => [value, next, this.tag].join(":");
                };
                const asyncExpression: any = async function*(this: any, next: number) {
                    await Promise.resolve(0);
                    yield () => [value, next, this.tag].join(":");
                };
                const object = {
                    tag: "object",
                    *values(next: number) { yield () => [value, next, this.tag].join(":"); },
                    async *asyncValues(next: number) {
                        await Promise.resolve(0);
                        yield () => [value, next, this.tag].join(":");
                    }
                };
                return { declaration, expression, asyncExpression, object };
            }
            async function run() {
                const first = make(47), second = make(53);
                const one: any = first.declaration().next().value;
                const two: any = second.declaration().next().value;
                console.log(one(), two());
                const expression: any = first.expression.call({ tag: "called" }, 5).next().value;
                const object: any = first.object.values(7).next().value;
                console.log(expression(), object());
                const asyncExpression: any = (await first.asyncExpression.call({ tag: "async" }, 9).next()).value;
                const asyncObject: any = (await first.object.asyncValues(11).next()).value;
                console.log(asyncExpression(), asyncObject());
            }
            run();
            """, "47 53\n47:5:called 47:7:object\n47:9:async 47:11:object\n"];
    }

    [Theory, ModeData]
    public void AnonymousFactoryGeneratorArrowKeepsItsEnclosingCapture(ExecutionMode mode)
        => Assert.Equal(Expected, TestHarness.Run(Source, mode));
}
