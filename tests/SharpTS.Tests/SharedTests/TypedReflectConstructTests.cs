using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class TypedReflectConstructTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "intrinsic-controls", """
            for (const target of [Function.prototype.call, Function.prototype.apply, Function.prototype.bind]) {
                try { Reflect.construct(target, []); console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            """, "true\ntrue\ntrue\n" };
        yield return new object[] { "original", """
            class Point{x:number;constructor(x:number){this.x=x;}}const p:any=Reflect.construct(Point,[7]);console.log(p.x,p instanceof Point);for(const value of [null,undefined,{},Function.prototype.call]){try{Reflect.construct(value as any,[]);console.log(false);}catch(error){console.log(error instanceof TypeError);}}
            """, "7 true\ntrue\ntrue\ntrue\ntrue\n" };
        yield return new object[] { "dynamic-control", """
            class Point{x:number;constructor(x:number){this.x=x;}}const p:any=Reflect.construct(Point as any,[7]);console.log(p.x,p instanceof Point);for(const value of [null,undefined,{},Function.prototype.call]){try{Reflect.construct(value as any,[]);console.log(false);}catch(error){console.log(error instanceof TypeError);}}

            """, "7 true\ntrue\ntrue\ntrue\ntrue\n" };
        yield return new object[] { "function-type-control", """
            class Point{x:number;constructor(x:number){this.x=x;}}
            function identity<T extends Function>(constructor:T):T{return constructor;}
            const broad:Function=Point;
            const signature:new (x:number)=>Point=Point;
            const a:any=Reflect.construct(identity(Point),[9]);
            const b:any=Reflect.construct(broad,[11]);
            const c:any=Reflect.construct(signature,[13]);
            console.log(a.x,a instanceof Point,b.x,b instanceof Point,c.x,c instanceof Point);
            """, "9 true 11 true 13 true\n" };
        yield return new object[] { "ordinary-functions", """
            function C() {}
            const created: any = Reflect.construct(C, []);
            console.log(typeof created, created instanceof C);
            console.log(new C() instanceof C);
            function Declared(this: any, value: any) { this.value = value; return 0; }
            const Expressed: any = function(this: any, value: any) { this.value = value; return null; };
            for (const target of [Declared, Expressed]) {
                const first: any = Reflect.construct(target, [7]);
                const second: any = Reflect.construct(target, [9]);
                console.log(first.value, second.value, first !== second, first instanceof target,
                    Object.getPrototypeOf(first) === target.prototype);
            }
            """, "object true\ntrue\n7 9 true true true\n7 9 true true true\n" };
        yield return new object[] { "ordinary-return-values", """
            function Returned(this: any, value: any) { this.marker = 7; return value; }
            const target: any = Returned;
            for (const value of [undefined, null, 0, false, "text"]) {
                const result: any = Reflect.construct(target, [value]);
                console.log(result.marker, result instanceof target);
            }
            for (const value of [{ tag: 3 }, [4, 5], () => 6]) {
                console.log(Reflect.construct(target, [value]) === value);
            }
            """, "7 true\n7 true\n7 true\n7 true\n7 true\ntrue\ntrue\ntrue\n" };
        yield return new object[] { "bound-functions", """
            function Value(this: any, first: any, second: any) { this.value = first * 10 + second; }
            const receiver: any = { value: 99 }, other: any = { value: 88 };
            const first: any = Value.bind(receiver, 2), second: any = first.bind(other, 3);
            const result: any = Reflect.construct(second, []);
            console.log(result.value, receiver.value, other.value, result !== receiver,
                result instanceof Value, Object.getPrototypeOf(result) === Value.prototype);
            const object: any = { value: 7 };
            function Returned() { return object; }
            console.log(Reflect.construct(Returned.bind(receiver), []) === object);
            """, "23 99 88 true true true\ntrue\n" };
        yield return new object[] { "ordinary-errors", """
            const token: any = { failure: true };
            function Failed(this: any) { this.value = 1; throw token; }
            const receiver: any = { value: 90 };
            try { Reflect.construct(Failed.bind(receiver), []); console.log(false); }
            catch (error) { console.log(error === token, receiver.value); }
            function Later(this: any) { this.value = 2; }
            console.log((Reflect.construct(Later.bind(receiver), []) as any).value, receiver.value);
            class C { method() {} }
            for (const target of [() => 1, new C().method]) {
                try { Reflect.construct(target, []); console.log(false); }
                catch (error) { console.log(error instanceof TypeError); }
            }
            """, "true 90\n2 90\ntrue\ntrue\n" };
    }

    public static IEnumerable<object[]> ParityCases() => Cases().Select(row => row[1..]);

    [Fact]
    public void InterpreterIgnoresSymbolAndBigIntConstructorReturns()
    {
        // Compiled Symbol/BigInt constructor returns are tracked in #2141.
        const string source = """
            function Returned(this: any, value: any) { this.marker = 7; return value; }
            for (const value of [Symbol("value"), 1n]) {
                const result: any = Reflect.construct(Returned, [value]);
                console.log(result.marker, result instanceof Returned);
            }
            """;
        Assert.Equal("7 true\n7 true\n", TestHarness.RunInterpreted(source));
    }

    [Theory, MemberData(nameof(ParityCases))]
    public void TypedConstructorValuesRetainIdentityAndInvalidTargetErrors(string source, string expected)
    {
        Assert.Equal(expected, TestHarness.Run(source, ExecutionMode.Interpreted));
        Assert.Equal(expected, TestHarness.Run(source, ExecutionMode.Compiled));
    }
}
