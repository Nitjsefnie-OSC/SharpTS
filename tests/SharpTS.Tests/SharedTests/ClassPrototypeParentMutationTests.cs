using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassPrototypeParentMutationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            class A { value(): any { return 'declared'; } }
            class B extends A { make() { return () => super.value; } }
            const home: any = B.prototype, read: any = new B().make();
            Object.setPrototypeOf(home, { value: 'switched' });
            console.log(read());
            Object.setPrototypeOf(home, null);
            try { read(); } catch (error) { console.log(error instanceof TypeError); }
            """, "switched\ntrue\n"];
        yield return ["reflect-restoration-and-static-parent", """
            class A { value(): any { return 'base'; } }
            class B extends A { make() { return () => super.value; } }
            const home: any = B.prototype, parent = Object.getPrototypeOf(home), read: any = new B().make();
            console.log(Reflect.setPrototypeOf(home, { value: 'first' }), read());
            console.log(Reflect.setPrototypeOf(home, parent), typeof read(), Object.getPrototypeOf(home) === parent);
            console.log(Object.getPrototypeOf(B) === A);
            """, "true first\ntrue function true\ntrue\n"];
        yield return ["lexical-home-and-captured-receiver", """
            class A { value(): any { return 'base'; } }
            class B extends A { make() { return () => super.value; } }
            class Leaf extends B { tag = 'leaf'; }
            const leaf: any = new Leaf(), borrowed: any = { tag: 'borrowed' };
            const read: any = leaf.make(), other: any = (B.prototype as any).make.call(borrowed);
            Object.defineProperty(B.prototype, 'value', { value: 'home-own', configurable: true });
            const state: any = { receiver: null };
            const parent: any = {};
            Object.defineProperty(parent, 'value', {
                get: function(this: any) { state.receiver = this; return this.tag; }
            });
            Object.setPrototypeOf(B.prototype, parent);
            console.log(read(), state.receiver === leaf);
            console.log(other(), state.receiver === borrowed);
            """, "leaf true\nborrowed true\n"];
        yield return ["ordinary-instance-chain", """
            class A { value(): any { return 'base'; } }
            class B extends A { tag = 'own'; }
            const value: any = new B();
            console.log(typeof value.value);
            Object.setPrototypeOf(B.prototype, { value: 'custom' });
            console.log(value.value, Object.getPrototypeOf(value) === B.prototype, value.tag, Reflect.get(value, 'tag'));
            console.log(value instanceof B, value instanceof A, value instanceof Object);
            """, "function\ncustom true own own\ntrue false true\n"];
        yield return ["generic-home", """
            class A<T> { value(): any { return 'base'; } }
            class B<T> extends A<T> { make() { return () => super.value; } }
            const read: any = new B<any>().make();
            Object.setPrototypeOf(B.prototype, { value: 'generic' });
            console.log(read());
            """, "generic\n"];
        yield return ["live-instance-accessors-and-own-fields", """
            class A {
                stored = 'own';
                get value(): any { return 'old'; }
                set value(next: any) { this.stored = 'old-setter'; }
            }
            class B extends A {}
            const instance: any = new B();
            console.log(instance.value);
            const parent: any = {};
            Object.defineProperty(parent, 'value', {
                get: function(this: any) { return this.stored; },
                set: function(this: any, next: any) { this.stored = next; }
            });
            Object.setPrototypeOf(B.prototype, parent);
            console.log(instance.value, Reflect.get(instance, 'stored'));
            instance.value = 'new';
            console.log(instance.value, Object.hasOwn(instance, 'value'));
            Object.setPrototypeOf(B.prototype, null);
            console.log(instance.value === undefined, instance.stored);
            """, "old\nown own\nnew false\ntrue new\n"];
        yield return ["closed-generic-home", """
            class A<T> { value(): any { return 'base'; } }
            class B<T> extends A<T> { make() { return () => super.value; } }
            const instance: any = new B<string>(), read: any = instance.make();
            const parent: any = Object.getPrototypeOf(B.prototype);
            console.log(Reflect.setPrototypeOf(B.prototype, Object.getPrototypeOf(instance)));
            Object.setPrototypeOf(B.prototype, { value: 'changed' });
            console.log(read(), instance.value, instance instanceof A);
            const later: any = new B<number>();
            console.log(later.value, later instanceof A);
            Object.setPrototypeOf(B.prototype, parent);
            console.log(typeof instance.value, instance instanceof A, later instanceof A);
            """, "false\nchanged changed false\nchanged false\nfunction true true\n"];
        yield return ["aliased-object-and-bracket-prototype", """
            class A { value(): any { return 'base'; } }
            class B extends A {}
            const instance: any = new B(), O: any = Object;
            const home: any = (B as any)['prototype'];
            O.setPrototypeOf(home, { value: 'changed' });
            console.log(instance.value, instance instanceof A);
            try { new B().value(); } catch (error) { console.log(error instanceof TypeError); }
            """, "changed false\ntrue\n"];
        yield return ["nonextensible-generic-home-aliases", """
            class A<T> { value(): any { return 'base'; } }
            class B<T> extends A<T> { make() { return () => super.value; } }
            const home: any = B.prototype;
            const instance: any = new B<string>();
            console.log(typeof instance.make()());
            const alias: any = Object.getPrototypeOf(instance);
            const parent: any = Object.getPrototypeOf(home);
            Object.preventExtensions(home);
            console.log(Reflect.setPrototypeOf(alias, parent));
            console.log(Reflect.setPrototypeOf(alias, {}), Object.getPrototypeOf(home) === parent);
            class C<T> extends A<T> { make() { return () => super.value; } }
            const otherHome: any = C.prototype;
            const otherInstance: any = new C<string>();
            console.log(typeof otherInstance.make()());
            const otherAlias: any = Object.getPrototypeOf(otherInstance);
            const otherParent: any = Object.getPrototypeOf(otherAlias);
            Object.preventExtensions(otherAlias);
            console.log(Reflect.setPrototypeOf(otherHome, otherParent));
            console.log(Reflect.setPrototypeOf(otherHome, {}), Object.getPrototypeOf(otherAlias) === otherParent);
            """, "function\ntrue\nfalse true\nfunction\ntrue\nfalse true\n"];
        yield return ["nonextensible-same-parent", """
            class A {}
            class B extends A {}
            const home: any = B.prototype, parent = Object.getPrototypeOf(home);
            console.log(Object.isExtensible(home));
            Object.preventExtensions(home);
            console.log(Object.isExtensible(home), Object.setPrototypeOf(home, parent) === home,
                Reflect.setPrototypeOf(home, parent));
            console.log(Reflect.setPrototypeOf(home, {}));
            try { Object.setPrototypeOf(home, {}); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(home) === parent);
            """, "true\nfalse true true\nfalse\ntrue\ntrue\n"];
        yield return ["cycle-rejection", """
            class A {}
            class B extends A {}
            const home: any = B.prototype, parent = Object.getPrototypeOf(home);
            console.log(Reflect.setPrototypeOf(home, home));
            const child: any = {};
            Object.setPrototypeOf(child, home);
            console.log(Reflect.setPrototypeOf(home, child));
            try { Object.setPrototypeOf(home, child); } catch (error) { console.log(error instanceof TypeError); }
            console.log(Object.getPrototypeOf(home) === parent);
            """, "false\nfalse\ntrue\ntrue\n"];
        yield return ["ordinary-chain-exhaustion", """
            class A { value(): any { return 'base'; } }
            class B extends A { make() { return () => super.value; } }
            const read: any = new B().make();
            Object.setPrototypeOf(B.prototype, Object.create(null));
            console.log(read() === undefined);
            Object.setPrototypeOf(B.prototype, null);
            try { read(); } catch (error) { console.log(error instanceof TypeError); }
            """, "true\ntrue\n"];
        yield return ["class-expression-control", """
            class A { value(): any { return 'base'; } }
            const B: any = class extends A { make() { return () => super.value; } };
            const read: any = new B().make();
            Object.setPrototypeOf(B.prototype, { value: 'expression' });
            console.log(read());
            Object.setPrototypeOf(B.prototype, null);
            try { read(); } catch (error) { console.log(error instanceof TypeError); }
            """, "expression\ntrue\n"];
    }

    public static IEnumerable<object[]> ParityCases()
        => Cases().SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ParityCases))]
    public void MutationsRetainLiveHomeObjects(string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        if (mode == ExecutionMode.Compiled)
        {
            var (errors, output) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            Assert.Equal(expected, output);
        }
        else
        {
            Assert.Equal(expected, TestHarness.Run(source, mode));
        }
    }
}
