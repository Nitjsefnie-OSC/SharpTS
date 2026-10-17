using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ComputedConstructorMethodTests
{
    public const string IssueSource = """
        class C { constructor() {} ["constructor"]() { return 44; } }
        const object: any = new C();
        console.log(object["constructor"]());
        """;

    public const string ConstructionSource = """
        let calls = "";
        class C {
            value: number;
            constructor(value: number) { this.value = value; calls += "c"; }
            ["constructor"]() { calls += "m"; return this.value + 44; }
        }
        const first: any = new C(3);
        console.log(calls, first.value);
        console.log(first["constructor"](), calls);
        class Reverse {
            value: number;
            ["constructor"]() { calls += "r"; return this.value + 45; }
            constructor(value: number) { this.value = value; calls += "d"; }
        }
        const second: any = new Reverse(4);
        console.log(calls, second.value);
        console.log(second["constructor"](), calls);
        """;

    public const string ExpressionSource = """
        let built = "";
        function create(offset: number) {
            return class C {
                value: number;
                constructor(value: number) { this.value = value; built += "c"; }
                ["constructor"]() { return offset + this.value; }
            };
        }
        const First = create(10), Second = create(20);
        const first: any = new First(1), second: any = new Second(2);
        console.log(built, first["constructor"](), second["constructor"]());
        console.log(first["constructor"] === (First.prototype as any)["constructor"],
            second["constructor"] === (Second.prototype as any)["constructor"]);
        """;

    public const string InheritanceSource = """
        let built = "";
        class Base {
            value: number;
            constructor(value: number) { this.value = value; built += "b"; }
            ["constructor"]() { return this.value + 40; }
        }
        class Derived extends Base {}
        const base: any = new Base(4), derived: any = new Derived(6);
        console.log(built, base["constructor"](), derived.value);
        console.log(derived["constructor"] === Derived,
            (Derived.prototype as any)["constructor"] === Derived,
            Object.getPrototypeOf(Derived.prototype) === Base.prototype);
        """;

    public const string DescriptorSource = """
        class C {
            constructor() {}
            ["constructor"]() { return 44; }
        }
        const object: any = new C(), prototype: any = C.prototype;
        const descriptor: any = Object.getOwnPropertyDescriptor(prototype, "constructor");
        console.log(object["constructor"] === prototype["constructor"],
            descriptor.value === prototype["constructor"], descriptor.value === C);
        console.log(descriptor.writable, descriptor.enumerable, descriptor.configurable);
        console.log(Reflect.get(object, "constructor") === prototype["constructor"]);
        """;

    public const string PreservationSource = """
        class Plain { value: number; constructor(value: number) { this.value = value; } }
        class Derived extends Plain {}
        const plain: any = new Plain(3), derived: any = new Derived(4);
        console.log(plain["constructor"] === Plain, derived["constructor"] === Derived,
            plain.value, derived.value);
        plain["constructor"] = "own";
        console.log(plain["constructor"], (new TypeError() as any)["constructor"] === TypeError);
        class StaticControl {
            constructor() {}
            static ["constructor"]() { return 45; }
        }
        const owner: any = StaticControl;
        console.log(owner.constructor(), owner["constructor"]());
        """;

    public const string OrdinaryPreservationSource = """
        class Plain { value: number; constructor(value: number) { this.value = value; } }
        class Derived extends Plain {}
        const plain: any = new Plain(3), derived: any = new Derived(4);
        console.log(plain.constructor === Plain, derived.constructor === Derived, plain.value, derived.value);
        Object.defineProperty(plain, "constructor", { value: "own", writable: true, configurable: true });
        console.log(plain.constructor, plain["constructor"], Object.getOwnPropertyDescriptor(plain, "constructor")!.value);
        const error: any = new TypeError();
        console.log(error.constructor === TypeError, error["constructor"] === TypeError);
        class StaticControl { constructor() {} static ["constructor"]() { return 45; } }
        const owner: any = StaticControl, staticInstance: any = new StaticControl();
        console.log(owner.constructor(), owner["constructor"](), staticInstance.constructor === owner);
        const cold: any = new Plain(5);
        cold["constructor"] = "cold";
        console.log(cold["constructor"] === "cold", cold.constructor === "cold",
            Object.getOwnPropertyDescriptor(cold, "constructor")!.value === "cold", Object.hasOwn(cold, "constructor"));
        """;

    public const string PrivateBrandSource = """
        const key = "constructor";
        class C {
            #value: number;
            constructor(value: number) { this.#value = value; }
            [key](extra: number) { return this.#value + extra; }
        }
        const first: any = new C(7), second: any = new C(9), prototype: any = C.prototype;
        const method: any = first[key];
        console.log(method === prototype[key], method === Object.getOwnPropertyDescriptor(prototype, key)!.value,
            first[key](1), method.call(second, 2));
        try { method.call({}, 1); }
        catch (error) { console.log(error instanceof TypeError); }
        try { new method(1); }
        catch (error) { console.log(error instanceof TypeError); }
        console.log(first[key](0), second[key](0));
        """;

    public const string DefinitionOrderSource = """
        const state = { keys: 0, built: "", reads: 0 };
        function key(): string { state.keys++; return "constructor"; }
        const receiverMethod = function(this: any) { return this; };
        class GetterLast {
            constructor() { state.built += "g"; }
            [key()]() { return "earlier"; }
            get [key()]() { state.reads++; return receiverMethod; }
        }
        class MethodLast {
            constructor() { state.built += "m"; }
            get [key()]() { state.reads++; throw new Error("replaced getter"); }
            [key()]() { return "later"; }
        }
        class Duplicate {
            constructor() { state.built += "d"; }
            [key()]() { return 1; }
            [key()]() { return 2; }
        }
        const first: any = new GetterLast(), second: any = new MethodLast(), third: any = new Duplicate();
        console.log(state.built, state.keys, state.reads);
        console.log(first.constructor() === first, state.reads);
        console.log(second["constructor"](), third.constructor(), state.reads, state.keys);
        const gd: any = Object.getOwnPropertyDescriptor(GetterLast.prototype, "constructor");
        const md: any = Object.getOwnPropertyDescriptor(MethodLast.prototype, "constructor");
        console.log(typeof gd.get, gd.set === undefined, typeof md.value, md.get === undefined);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue", IssueSource, "44\n"];
        yield return ["construction-separation", ConstructionSource, "c 3\n47 cm\ncmd 4\n49 cmdr\n"];
        yield return ["class-expression", ExpressionSource, "cc 11 22\ntrue true\n"];
        yield return ["inheritance", InheritanceSource, "bb 44 6\ntrue true true\n"];
        yield return ["prototype-descriptor", DescriptorSource, "true true false\ntrue false true\ntrue\n"];
        yield return ["preservation-controls", PreservationSource, "true true 3 4\nown true\n45 45\n"];
        yield return ["ordinary-preservation", OrdinaryPreservationSource, "true true 3 4\nown own own\ntrue true\n45 45 true\ntrue true true true\n"];
        yield return ["private-brand", PrivateBrandSource, "true true 8 11\ntrue\ntrue\n7 9\n"];
        yield return ["definition-order", DefinitionOrderSource, "gmd 6 0\ntrue 1\nlater 2 1 6\nfunction true function true\n"];
    }

    public static IEnumerable<object[]> ModeCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { mode, row[0], row[1], row[2] }));

    [Theory, MemberData(nameof(ModeCases))]
    public void ComputedConstructorMethodsKeepRealConstructionSeparateAndUseLivePrototypeValues(
        ExecutionMode mode, string name, string source, string expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
