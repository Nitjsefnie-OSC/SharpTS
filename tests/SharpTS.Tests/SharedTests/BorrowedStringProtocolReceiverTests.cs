using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class BorrowedStringProtocolReceiverTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "borrowed_receiver", """
            const value:any={[Symbol.match](s:any){return typeof s+":"+s;}};console.log(String.prototype.match.call(77 as any,value));
            """, "number:77\n" };
        yield return new object[] { "object-identity-without-conversion", """
            const receiver:any={toString(){throw "unexpected";}};const value:any={[Symbol.match](s:any){console.log(this===value,s===receiver);return "hook";}};console.log(String.prototype.match.call(receiver,value));
            """, "true true\nhook\n" };
        yield return new object[] { "getter-before-call", """
            let order="";const receiver:any={toString(){order+="string>";return "abc";}};const value:any={};Object.defineProperty(value,Symbol.match,{get(){order+="get>";return function(s:any){order+="call>";return s===receiver;};}});console.log(String.prototype.match.call(receiver,value),order);
            """, "true get>call>\n" };
        yield return new object[] { "fallback-coercion-order", """
            let order="";const receiver:any={toString(){order+="receiver>";return "abc";}};const value:any={toString(){order+="pattern>";return "b";}};Object.defineProperty(value,Symbol.match,{get(){order+="get>";return null;}});const result:any=String.prototype.match.call(receiver,value);console.log(result[0],order);
            """, "b get>receiver>pattern>\n" };
        yield return new object[] { "abrupt-coercion-order", """
            let order="";const original=new Error("original");const receiver:any={toString(){order+="receiver>";throw original;}};const value:any={toString(){order+="pattern>";return "b";}};Object.defineProperty(value,Symbol.match,{get(){order+="get>";return undefined;}});try{String.prototype.match.call(receiver,value);}catch(e){console.log(e===original,order);}
            """, "true get>receiver>\n" };
        yield return new object[] { "abrupt-getter-precedes-conversion", """
            let calls=0;const original=new Error("original");const receiver:any={toString(){calls++;return "abc";}};const value:any={};Object.defineProperty(value,Symbol.match,{get(){throw original;}});try{String.prototype.match.call(receiver,value);}catch(e){console.log(e===original,calls);}
            """, "true 0\n" };
        yield return new object[] { "primitive-receiver-types", """
            const value:any={[Symbol.match](s:any){return typeof s;}};const match:any=String.prototype.match;for(const receiver of [77,false,"abc",1n,Symbol("s")]){console.log(match.call(receiver,value));}
            """, "number\nboolean\nstring\nbigint\nsymbol\n" };
        yield return new object[] { "borrowed-value-forms", """
            const value:any={[Symbol.match](s:any){return typeof s+":"+s;}};const match:any=String.prototype.match;console.log(match.call(77,value),match.apply(77,[value]),match.bind(77,value)());
            """, "number:77 number:77 number:77\n" };
        yield return new object[] { "replace-original-receiver-and-replacement", """
            const receiver:any={toString(){throw "unexpected receiver coercion";}};const replacement:any={toString(){throw "unexpected replacement coercion";}};const value:any={[Symbol.replace](s:any,r:any){console.log(this===value,s===receiver,r===replacement);return 7;}};const replace:any=String.prototype.replace;console.log(replace.call(receiver,value,replacement),replace.apply(receiver,[value,replacement]),replace.bind(receiver,value,replacement)());const primitive:any={[Symbol.replace](s:any){return typeof s+":"+s;}};console.log(replace.call(77,primitive,"X"));
            """, "true true true\ntrue true true\ntrue true true\n7 7 7\nnumber:77\n" };
        yield return new object[] { "replace-fallback-coercion-order", """
            let order="";const receiver:any={toString(){order+="receiver>";return "abc";}};const value:any={toString(){order+="search>";return "b";}};Object.defineProperty(value,Symbol.replace,{get(){order+="get>";return null;}});const replacement:any={toString(){order+="replacement>";return "X";}};console.log(String.prototype.replace.call(receiver,value,replacement),order);order="";console.log(String.prototype.replace.call(receiver,value,function(m:any,i:any,s:any){order+="call>";console.log(m,i,s);return "X";}),order);console.log(String.prototype.replace.call(77,"7"));
            """, "aXc get>receiver>search>replacement>\nb 1 abc\naXc get>receiver>search>call>\nundefined7\n" };
        yield return new object[] { "replace-abrupt-protocol-order", """
            let order="";const original=new Error("original");const receiver:any={toString(){order+="receiver>";throw original;}};const value:any={toString(){order+="search>";return "b";}};Object.defineProperty(value,Symbol.replace,{get(){order+="get>";return undefined;},configurable:true});try{String.prototype.replace.call(receiver,value,"X");}catch(e){console.log(e===original,order);}order="";Object.defineProperty(value,Symbol.replace,{get(){order+="get>";throw original;}});try{String.prototype.replace.call(receiver,value,"X");}catch(e){console.log(e===original,order);}
            """, "true get>receiver>\ntrue get>\n" };
        yield return new object[] { "replace-native-nullish-hook-string-fallback", """
            const replace:any=String.prototype.replace;let order="";const receiver:any={toString(){order+="receiver>";return "abcz";}};const value:any=/b/;value.toString=function(){order+="search>";return "z";};const hooks:any[]=[null,undefined];for(const hook of hooks){Object.defineProperty(value,Symbol.replace,{get(){order+="get>";return hook;},configurable:true});order="";console.log(replace.call(receiver,value,"X"),order);order="";console.log(replace.call(receiver,value,function(m:any,i:any,s:any){order+="call>";console.log(m,i,s);return "X";}),order);}console.log("abc".replace(/b/,"X"));
            """, "abcX get>receiver>search>\nz 3 abcz\nabcX get>receiver>search>call>\nabcX get>receiver>search>\nz 3 abcz\nabcX get>receiver>search>call>\naXc\n" };
        yield return new object[] { "nullish-receiver-before-getter", """
            let calls=0;const value:any={};for(const key of [Symbol.match,Symbol.search,Symbol.matchAll,Symbol.split,Symbol.replace]){Object.defineProperty(value,key,{get(){calls++;return function(){return "hook";};}});}const p:any=String.prototype;for(const method of [p.match,p.search,p.matchAll,p.split,p.replace,p.replaceAll]){for(const receiver of [null,undefined]){try{method.call(receiver,value);}catch(e){console.log(e.name,e instanceof TypeError,calls);}}}
            """, "TypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\nTypeError true 0\n" };
        yield return new object[] { "other-protocol-original-receivers", """
            let calls=0;const receiver:any={toString(){calls++;throw "unexpected";}};const value:any={[Symbol.search](s:any){return s===receiver;},[Symbol.matchAll](s:any){return s===receiver;},[Symbol.split](s:any,n:any){console.log(n);return s===receiver;},[Symbol.replace](s:any,r:any){console.log(r);return s===receiver;}};const p:any=String.prototype;console.log(p.search.call(receiver,value),p.matchAll.call(receiver,value),p.split.call(receiver,value,2),p.replaceAll.call(receiver,value,"X"),calls);
            """, "2\nX\ntrue true true true 0\n" };
        yield return new object[] { "boxed-string-direct-and-any", """
            const receiver=new String("abc");const alias:any=receiver;const value:any={flags:"g",[Symbol.match](s:any){console.log(s===receiver);return ["M"];},[Symbol.search](s:any){console.log(s===receiver);return 7;},[Symbol.matchAll](s:any){console.log(s===receiver);return "A";},[Symbol.split](s:any){console.log(s===receiver);return ["S"];}};const result:any=receiver.match(value);console.log(result[0],alias.match(value)[0]);console.log(receiver.search(value),alias.search(value));console.log(receiver.split(value)[0],alias.split(value)[0]);console.log((String.prototype as any).matchAll.call(receiver,value),alias.matchAll(value));
            """, "true\ntrue\nM M\ntrue\ntrue\n7 7\ntrue\ntrue\nS S\ntrue\ntrue\nA A\n" };
        yield return new object[] { "replace-boxed-string-direct-and-any", """
            const receiver=new String("abc");const alias:any=receiver;const replacement:any={};const value:any={[Symbol.replace](s:any,r:any){console.log(s===receiver,r===replacement);return "hook";}};console.log(receiver.replace(value,replacement),alias.replace(value,replacement));
            """, "true true\ntrue true\nhook hook\n" };
        yield return new object[] { "ordinary-fallback-and-omitted-pattern", """
            const match:any=String.prototype.match;const search:any=String.prototype.search;console.log(match.call(77,"7")[0],search.call(77,"7"));const empty:any=match.call("abc");console.log(empty[0]==="",empty.index,search.call("abc"));for(const method of [match,search]){try{method.call(Symbol("s"),undefined);}catch(e){console.log(e.name,e instanceof TypeError);}}
            """, "7 0\ntrue 0 0\nTypeError true\nTypeError true\n" };
    }

    public static IEnumerable<object[]> SourceCases() => Cases().Select(row => row[1..]);
    public static IEnumerable<object[]> HostedCases() => Cases().Select(row => row[..2]);

    [Theory, MemberData(nameof(SourceCases))]
    public void BorrowedStringProtocolsDeferReceiverConversion(string source, string expected)
    {
        Assert.Equal(expected, TestHarness.Run(source, ExecutionMode.Compiled));
    }

    [Theory, ModeData]
    public void ReplaceNativeNullishProtocolUsesStringFallback(ExecutionMode mode)
    {
        var row = Cases().Single(row => (string)row[0] == "replace-native-nullish-hook-string-fallback");
        Assert.Equal((string)row[2], TestHarness.Run((string)row[1], mode));
    }

    [Theory, ModeData]
    public void ReplaceProtocolPreservesReceiverAndReplacementInBorrowedCalls(ExecutionMode mode)
    {
        const string source = """
            const receiver: any = { toString() { throw "unexpected receiver conversion"; } };
            const replacement: any = { toString() { throw "unexpected replacement conversion"; } };
            const pattern: any = {};
            let order = "";
            Object.defineProperty(pattern, Symbol.replace, {
                get() {
                    order += "get>";
                    return function(value: any, substitute: any) {
                        order += "call>";
                        console.log(this === pattern, value === receiver, substitute === replacement);
                        return "hook";
                    };
                }
            });
            const replace: any = String.prototype.replace;
            console.log(replace.call(receiver, pattern, replacement),
                replace.apply(receiver, [pattern, replacement]),
                replace.bind(receiver, pattern, replacement)(), order);
            """;

        Assert.Equal("true true true\ntrue true true\ntrue true true\nhook hook hook get>call>get>call>get>call>\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ReplaceProtocolPreservesPrimitiveReceiverTypes(ExecutionMode mode)
    {
        const string source = """
            const pattern: any = { [Symbol.replace](value: any) { return typeof value; } };
            const replace: any = String.prototype.replace;
            for (const receiver of [77, false, "abc", 1n, Symbol("s")]) {
                console.log(replace.call(receiver, pattern));
            }
            """;

        Assert.Equal("number\nboolean\nstring\nbigint\nsymbol\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ReplaceFallbackConvertsReceiverAfterProtocolLookup(ExecutionMode mode)
    {
        const string source = """
            let order = "";
            const receiver: any = { toString() { order += "receiver>"; return "abc"; } };
            const pattern: any = { toString() { order += "pattern>"; return "b"; } };
            const replacement: any = { toString() { order += "replacement>"; return "X"; } };
            Object.defineProperty(pattern, Symbol.replace, { get() { order += "get>"; return null; } });
            const replace: any = String.prototype.replace;
            console.log(replace.call(receiver, pattern, replacement), order);
            order = "";
            console.log(replace.call(receiver, pattern, function(matched: any, index: any, value: any) {
                order += "call>";
                console.log(matched, index, value);
                return "Y";
            }), order);
            console.log(replace.call("undefined"), replace.call("abc", "b"), replace.call("abc", "z", "X"));
            """;

        Assert.Equal("aXc get>receiver>pattern>replacement>\nb 1 abc\naYc get>receiver>pattern>call>\nundefined aundefinedc abc\n",
            TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ReplaceAbruptLookupAndReceiverConversionKeepObservableOrder(ExecutionMode mode)
    {
        const string source = """
            let order = "";
            const original = new Error("original");
            const receiver: any = { toString() { order += "receiver>"; throw original; } };
            const abruptPattern: any = {};
            Object.defineProperty(abruptPattern, Symbol.replace, { get() { order += "get>"; throw original; } });
            const replace: any = String.prototype.replace;
            try { replace.call(receiver, abruptPattern, "X"); }
            catch (error) { console.log(error === original, order); }
            order = "";
            const fallbackPattern: any = { toString() { order += "pattern>"; return "b"; } };
            Object.defineProperty(fallbackPattern, Symbol.replace, { get() { order += "get>"; return undefined; } });
            try { replace.call(receiver, fallbackPattern, "X"); }
            catch (error) { console.log(error === original, order); }
            """;

        Assert.Equal("true get>\ntrue get>receiver>\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ReplaceRejectsNullishReceiversBeforeProtocolLookup(ExecutionMode mode)
    {
        const string source = """
            let calls = 0;
            const pattern: any = {};
            Object.defineProperty(pattern, Symbol.replace, { get() { calls++; return function() { return "hook"; }; } });
            const replace: any = String.prototype.replace;
            for (const receiver of [null, undefined]) {
                try { replace.call(receiver, pattern, "X"); }
                catch (error) { console.log(error.name, error instanceof TypeError, calls); }
            }
            """;

        Assert.Equal("TypeError true 0\nTypeError true 0\n", TestHarness.Run(source, mode));
    }
}
