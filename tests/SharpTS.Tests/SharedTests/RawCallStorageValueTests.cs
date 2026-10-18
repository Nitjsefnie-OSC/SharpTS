using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class RawCallStorageValueTests
{
    public const string Exact = """
        function raw(): number { return null as any; }
        function local(): number {
            let value: number = 1;
            value = raw();
            console.log("stored", value);
            return value;
        }
        console.log("returned", local());
        """;

    public const string StorageAndOwners = """
        function rawNumber(): number { return null as any; }
        function rawFlag(): boolean { return undefined as any; }
        function observe(value: number, flag: boolean): void {
            let initialized: number = rawNumber();
            let inferred = rawNumber();
            const constant: number = rawNumber();
            let booleanLocal: boolean = rawFlag();
            value = rawNumber();
            flag = rawFlag();
            console.log("void", initialized, inferred, constant, booleanLocal, value, flag);
        }
        const arrow = (value: number): number => {
            let stored: number = value;
            stored = rawNumber();
            console.log("arrow", stored);
            return stored;
        };
        const expression = function(value: boolean): boolean {
            value = rawFlag();
            console.log("expression", value);
            return value;
        };
        class Owner {
            constructor() {
                let stored: number = rawNumber();
                console.log("constructor", stored);
            }
            method(): number {
                let stored: number = rawNumber();
                console.log("method", stored);
                return stored;
            }
            get value(): number {
                let stored: number = rawNumber();
                console.log("getter", stored);
                return stored;
            }
            set value(input: number) {
                let stored: number = input;
                stored = rawNumber();
                console.log("setter", stored);
            }
        }
        observe(1, true);
        console.log("arrow-result", arrow(1));
        console.log("expression-result", expression(true));
        const owner = new Owner();
        console.log("method-result", owner.method());
        console.log("getter-result", owner.value);
        owner.value = 1;
        """;

    public const string JointFlowAndPassthrough = """
        function first(): number { return middle(); }
        function middle(): number {
            let stored: number = raw();
            const copied: number = stored;
            return copied;
        }
        function raw(): number { return null as any; }
        function rawText(): number { return "kept" as any; }
        function loop(): number {
            let value: number = 5;
            let copied: number = value;
            for (let index = 0; index < 2; index++) {
                copied = value;
                value = raw();
            }
            return copied;
        }
        function choose(outer: boolean, inner: boolean): number {
            let stored: number = raw();
            const nested: number = ((outer ? (inner ? stored : 1) : (inner ? 2 : stored)) as number)!;
            return nested;
        }
        function logical(): void {
            let target: number = rawText();
            let result: number = (target ||= 7);
            console.log("logical", target, result);
        }
        console.log("joint", first(), loop(), choose(true, true), choose(false, false));
        logical();
        """;

    public const string BindingIsolationAndConversions = """
        function raw(): number { return null as any; }
        function safe(): number { return 7; }
        function outer(input: number): number {
            const read = () => input;
            { let value: number = safe(); console.log("clean-block", value); }
            function child(value: number): number {
                let local: number = safe();
                return value + local;
            }
            { let value: number = raw(); console.log("raw-block", value); }
            let captured: number = 1;
            const mutate = () => { captured = raw(); };
            const readCaptured = () => captured;
            mutate();
            console.log("capture", readCaptured(), captured);
            console.log("outer", input, read(), child(2));
            return input;
        }
        function conversions(): void {
            const plus: number = +raw();
            const arithmetic: number = raw() + 2;
            const compared: boolean = raw() === 0;
            console.log("coerce", plus, arithmetic, compared);
        }
        console.log("clean-return", outer(5));
        conversions();
        """;

    public const string RewrittenAndDefaultOwners = """
        function raw(): number { return null as any; }
        function* make(seed: number) {
            function lifted(value: number = raw()): number {
                const copied: number = value;
                console.log("default", copied);
                return copied;
            }
            function captured(value: number): number {
                value = raw();
                const copied: number = value;
                console.log("capture", seed, copied);
                return copied;
            }
            return { lifted, captured };
        }
        function defaultCallback(callback: () => number = () => {
            let stored: number = raw();
            return stored;
        }): number { return callback(); }
        const first = make(10).next().value, second = make(20).next().value;
        console.log("lifted", first.lifted(), second.lifted());
        console.log("captured", first.captured(1), second.captured(2));
        const expression = (value: number): number => (value = raw());
        console.log("expression", expression(1));
        console.log("callback", defaultCallback());
        new (class {
            constructor() {
                let stored: number = raw();
                console.log("callee", stored);
            }
        })();
        """;

    public const string CustomIteratorRawStorage = """
        function raw(): number { return null as any; }
        function unsafe(mode: number): void {
            let current: number = 0;
            const iterable = {
                [Symbol.iterator]() { return this; },
                next() {
                    if (current < 2) {
                        const value: number = current;
                        current = current + 1;
                        return { value, done: false };
                    }
                    return { value: 0, done: true };
                }
            };
            let total: number = mode === 0 ? raw() : 0;
            console.log("start", total);
            for (const value of iterable) total = total + value;
            console.log("finish", total);
            if (mode === 1) {
                const copied: number = raw();
                total = copied;
                console.log("later", total);
            }
        }
        function safe(): number {
            let current: number = 0;
            const iterable = {
                [Symbol.iterator]() { return this; },
                next() {
                    if (current < 2) {
                        const value: number = current;
                        current = current + 1;
                        return { value, done: false };
                    }
                    return { value: 0, done: true };
                }
            };
            let total: number = 0;
            for (const value of iterable) total = total + value;
            return total;
        }
        unsafe(0);
        unsafe(1);
        console.log("safe", safe());
        """;

    public const string VirtualParameterSlots = """
        function rawNumber(): number { return null as any; }
        function rawBoolean(): boolean { return null as any; }
        class NumberBase { run(value: number): any { return value; } }
        class NumberChild extends NumberBase {
            run(value: number): any { value = rawNumber(); return value; }
        }
        class BooleanBase { run(value: boolean): any { return value; } }
        class BooleanChild extends BooleanBase {
            run(value: boolean): any { value = rawBoolean(); return value; }
        }
        class GenericBase<T> { run(value: number): any { return value; } }
        class GenericChild<T> extends GenericBase<T> {
            run(value: number): any { value = rawNumber(); return value; }
        }
        class PrimitiveControl { run(value: number): number { return value + 0; } }
        function numberCall(value: NumberBase): any { return value.run(7); }
        function booleanCall(value: BooleanBase): any { return value.run(true); }
        function genericCall(value: GenericBase<string>): any { return value.run(3); }
        console.log("number", new NumberChild().run(7), numberCall(new NumberChild()));
        console.log("boolean", new BooleanChild().run(true), booleanCall(new BooleanChild()));
        console.log("generic", new GenericChild<string>().run(3), genericCall(new GenericChild<string>()));
        console.log("safe", new PrimitiveControl().run(5));
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-reassignment", Exact, "stored null\nreturned null\n"];
        yield return ["storage-and-owners", StorageAndOwners, "void null null null undefined null undefined\narrow null\narrow-result null\nexpression undefined\nexpression-result undefined\nconstructor null\nmethod null\nmethod-result null\ngetter null\ngetter-result null\nsetter null\n"];
        yield return ["joint-flow-and-passthrough", JointFlowAndPassthrough, "joint null null null null\nlogical kept kept\n"];
        yield return ["binding-isolation-and-conversions", BindingIsolationAndConversions, "clean-block 7\nraw-block null\ncapture null null\nouter 5 5 9\nclean-return 5\ncoerce 0 2 false\n"];
        yield return ["rewritten-and-default-owners", RewrittenAndDefaultOwners, "default null\ndefault null\nlifted null null\ncapture 10 null\ncapture 20 null\ncaptured null null\nexpression null\ncallback null\ncallee null\n"];
        yield return ["custom-iterator-raw-storage", CustomIteratorRawStorage, "start null\nfinish 1\nstart 0\nfinish 1\nlater null\nsafe 1\n"];
        yield return ["virtual-parameter-slots", VirtualParameterSlots, "number null null\nboolean null null\ngeneric null null\nsafe 5\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void RawCallsPreserveStoredValues(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
