using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class PrimitiveStringSymbolHookTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "primitive-prototype-hook-ignored", """
            (Number.prototype as any)[Symbol.search]=function(s:string){return s.length+4;};console.log("a1b".search(1 as any));
            """, "1\n" };
        yield return new object[] { "five-primitive-prototype-hooks-ignored", """
            let calls=0;for(const prototype of [Number.prototype,Boolean.prototype,String.prototype,BigInt.prototype,Symbol.prototype]){Object.defineProperty(prototype,Symbol.search,{get(){calls++;throw "unexpected";}});}for(const value of [1,false,"x",1n]){console.log("1 false x 1".search(value as any));}try{"abc".search(Symbol("s") as any);}catch(e){console.log(e instanceof TypeError);}console.log(calls);
            """, "0\n2\n8\n0\ntrue\n0\n" };
        yield return new object[] { "strict-boxed-accessor-receiver", """
            const box:any=new Number(2);let order="";Object.defineProperty(Number.prototype,Symbol.search,{get(){"use strict";order+="get:"+typeof this+":"+(this===box)+">";return function(s:any){"use strict";order+="call:"+typeof this+":"+(this===box)+">";return s.length;};}});console.log("abc".search(box),order);
            """, "3 get:object:true>call:object:true>\n" };
        yield return new object[] { "strict-boxed-string-accessor-receiver", """
            const box:any=new String("b");Object.defineProperty(String.prototype,Symbol.match,{get(){"use strict";console.log(typeof this,this===box);return function(s:any){"use strict";console.log(typeof this,this===box);return [s];};}});console.log("abc".match(box)![0]);
            """, "object true\nobject true\nabc\n" };
        yield return new object[] { "boxed-abrupt-accessor-and-call", """
            const box:any=new Boolean(false);const original=new Error("original");Object.defineProperty(Boolean.prototype,Symbol.search,{get(){throw original;},configurable:true});try{"abc".search(box);}catch(e){console.log(e===original);}delete (Boolean.prototype as any)[Symbol.search];(Boolean.prototype as any)[Symbol.search]=function(){throw original;};try{"abc".search(box);}catch(e){console.log(e===original);}
            """, "true\ntrue\n" };
        yield return new object[] { "boxed-noncallable-and-nullish-hooks", """
            const box:any=new Number(1);const p:any=Number.prototype;for(const value of [false,0,"x",{}]){p[Symbol.search]=value;try{"abc".search(box);}catch(e){console.log(e.name,e instanceof TypeError);}}p[Symbol.search]=null;console.log("a1b".search(box));p[Symbol.search]=undefined;console.log("a1b".search(box));delete p[Symbol.search];console.log("a1b".search(box));
            """, "TypeError true\nTypeError true\nTypeError true\nTypeError true\n1\n1\n1\n" };
        yield return new object[] { "boxed-all-shared-string-protocols", """
            const box:any=new Number(1);const p:any=Number.prototype;p[Symbol.match]=function(s:any){return ["M",s];};p[Symbol.search]=function(s:any){return s.length+4;};p[Symbol.replace]=function(s:any,r:any){return s+":"+r;};p[Symbol.split]=function(s:any,n:any){return [s,String(n)];};console.log("abc".match(box)!.join("|"),"abc".search(box),"abc".replace(box,"X"),"abc".replaceAll(box,"Y"),"abc".split(box,2).join("|"));
            """, "M|abc 7 abc:X abc:Y abc|2\n" };
        yield return new object[] { "matchall-primitive-hooks-ignored", """
            Object.defineProperty(Number.prototype,Symbol.match,{get(){throw "unexpected match";}});Object.defineProperty(Number.prototype,Symbol.matchAll,{get(){throw "unexpected matchAll";}});const matches:any=Array.from("a1b1c".matchAll(1 as any));console.log(matches.length,matches[0][0],matches[0].index,matches[1].index);
            """, "2 1 1 3\n" };
        yield return new object[] { "boxed-and-custom-controls", """
            const box:any=new Number(1);(Number.prototype as any)[Symbol.search]=function(s:any){"use strict";console.log(this===box,typeof this);return s.length+4;};console.log("abc".search(box));const value:any={[Symbol.match](s:any){console.log(this===value);return [s];}};console.log("abc".match(value)![0]);
            """, "true object\n7\ntrue\nabc\n" };
        yield return new object[] { "nullish-candidates-skip-prototypes", """
            let calls=0;(Object.prototype as any)[Symbol.search]=function(){calls++;return 99;};console.log("abc".search(undefined),"null".search(null),calls);
            """, "0 0 0\n" };
        yield return new object[] { "boxed-inherited-object-prototype-hook", """
            (Object.prototype as any)[Symbol.search]=function(s:any){"use strict";console.log(typeof this);return s.length+5;};console.log("abc".search(new Number(1) as any),"abc".search(new Boolean(false) as any),"abc".search(new String("x") as any));
            """, "object\nobject\nobject\n8 8 8\n" };
        yield return new object[] { "borrowed-boxed-value-calls", """
            const box:any=new Number(1);(Number.prototype as any)[Symbol.search]=function(s:any){"use strict";console.log(typeof this,this===box);return s.length+4;};const search:any=String.prototype.search;console.log(search.call("abc",box),search.apply("abc",[box]),search.bind("abc",box)());
            """, "object true\nobject true\nobject true\n7 7 7\n" };
    }

    public static IEnumerable<object[]> SourceCases() => Cases().Select(row => row[1..]);
    public static IEnumerable<object[]> HostedCases() => Cases().Select(row => row[..2]);

    [Theory, MemberData(nameof(SourceCases))]
    public void StringProtocolsDistinguishPrimitiveAndObjectCandidates(string source, string expected)
    {
        Assert.Equal(expected, TestHarness.Run(source, ExecutionMode.Compiled));
    }
}
