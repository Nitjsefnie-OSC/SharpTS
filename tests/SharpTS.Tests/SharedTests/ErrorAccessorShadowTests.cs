using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ErrorAccessorShadowTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-and-dispatch", """
            class C extends Error { set message(next: string) {} }
            const absent: any = new C(), own: any = new C("own");
            console.log(JSON.stringify(new C().message), JSON.stringify(new C("own").message));
            console.log(absent.message === undefined, absent["message"] === undefined,
                Reflect.get(absent, "message") === undefined, own.message, Reflect.get(own, "message"));
            console.log(Object.hasOwn(absent, "message"), Object.hasOwn(own, "message"));
            """, "undefined \"own\"\ntrue true true own own\nfalse true\n"];
        yield return ["expressions-and-native-kinds", """
            const C = class extends TypeError {
                constructor(message?: string) { super(message); }
                set message(next: string) {} set name(next: string) {}
            };
            class Parent extends TypeError { set message(next: string) {} set name(next: string) {} }
            class D extends Parent {}
            const absent: any = new C(), own: any = new C("own"), inherited: any = new D("inherited");
            console.log(absent.message === undefined, absent.name === undefined, own.message, own.name === undefined);
            console.log(absent instanceof C, absent instanceof TypeError, inherited instanceof Error,
                inherited.message, inherited.name === undefined);
            """, "true true own true\ntrue true true inherited true\n"];
        yield return ["getters", """
            let reads = 0;
            class C extends Error { get message() { reads++; return "getter"; } }
            const absent: any = new C(), own: any = new C("own");
            console.log(absent.message, own.message, reads);
            """, "getter own 1\n"];
        yield return ["native-defaults", """
            class D extends Error {}
            class T extends TypeError {}
            const plain: any = new D();
            const typed: any = new T("typed");
            console.log(JSON.stringify(plain.message), plain.name, Object.hasOwn(plain, "message"));
            console.log(typed.name, typed.message);
            """, "\"\" Error false\nTypeError typed\n"];
        yield return ["native-prototype-defaults", """
            class E extends Error {}
            class T extends TypeError {}
            class D extends T {}
            class G<T> extends TypeError {}
            const Expression = class extends TypeError { constructor(message?: string) { super(message); } };
            for (const owner of [E, T, D, G, Expression]) {
                const prototype: any = (owner as any).prototype;
                console.log(prototype.name, JSON.stringify(prototype.message), typeof prototype.toString);
            }
            console.log(Object.getPrototypeOf(E.prototype) === Error.prototype,
                Object.getPrototypeOf(T.prototype) === TypeError.prototype,
                Object.getPrototypeOf(D.prototype) === T.prototype);
            """, "Error \"\" function\nTypeError \"\" function\nTypeError \"\" function\nTypeError \"\" function\nTypeError \"\" function\ntrue true true\n"];
        yield return ["object-prototype-defaults", """
            Object.defineProperty(Object.prototype, "message", { value: "polluted", configurable: true });
            Object.defineProperty(Object.prototype, "name", { value: "polluted", configurable: true });
            Object.defineProperty(Object.prototype, "extra", { value: "inherited", configurable: true });
            class E extends Error {}
            class T extends TypeError {}
            const plain: any = new E(), typed: any = new T();
            const prototype: any = E.prototype;
            console.log(plain.name, JSON.stringify(plain.message), typed.name, JSON.stringify(typed.message));
            console.log(prototype.name, JSON.stringify(prototype.message), plain.extra, prototype.extra);
            """, "Error \"\" TypeError \"\"\nError \"\" inherited inherited\n"];
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var row in Cases())
        {
            // Inherited native Error prototype defaults are absent in interpretation (#2295).
            if ((string)row[0] is not ("native-defaults" or "native-prototype-defaults" or "object-prototype-defaults"))
                yield return [row[0], row[1], row[2], ExecutionMode.Interpreted];
            yield return [row[0], row[1], row[2], ExecutionMode.Compiled];
        }
    }

    [Theory, MemberData(nameof(ModeCases))]
    public void ErrorOwnMessagesPrecedeAccessorsAndAccessorsPrecedeDefaults(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
