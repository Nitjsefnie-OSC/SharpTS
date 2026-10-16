using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class ClassMethodThisCoercionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["original", """
            const C = class { value = 7; read() { return this.value; } };
            const instance: any = new C(), read: any = instance.read;
            try { console.log('bare returned', read()); }
            catch (error: any) { console.log('bare threw', error instanceof TypeError); }
            function invoke(name: string, receiver: any) {
                try { console.log(name, 'returned', read.call(receiver)); }
                catch (error: any) { console.log(name, 'threw', error instanceof TypeError); }
            }
            invoke('undefined', undefined);
            invoke('null', null);
            invoke('object', { value: 13 });
            """, "bare threw true\nundefined threw true\nnull threw true\nobject returned 13\n"];
        yield return ["declared-class-and-explicit-invocation", """
            class C { value = 7; read() { return this.value; } }
            const read: any = (C.prototype as any).read;
            try { read.apply(undefined, []); } catch (error) { console.log(error instanceof TypeError); }
            try { read.bind(null)(); } catch (error) { console.log(error instanceof TypeError); }
            console.log(read.apply({ value: 11 }, []));
            """, "true\ntrue\n11\n"];
        yield return ["optional-and-raw-this-controls", """
            const C: any = class {
                value = 7;
                optional() { return this?.value; }
                self() { return this; }
            };
            const instance: any = new C(), read: any = instance.optional, self: any = instance.self;
            console.log(read.call(undefined) === undefined, read.call(null) === undefined);
            console.log(self.call(undefined) === undefined, self.call(null) === null);
            console.log(read.call({ value: 13 }));
            """, "true true\ntrue true\n13\n"];
        yield return ["primitive-receivers", """
            const C: any = class { value = 7; read() { return this.value; } size() { return (this as any).length; } };
            const instance: any = new C(), read: any = instance.read, size: any = instance.size;
            console.log(read.call(0) === undefined, read.call(false) === undefined, read.call('') === undefined);
            console.log(size.call(''), size.call('abcd'));
            """, "true true true\n0 4\n"];
        yield return ["computed-read-control", """
            const C: any = class { value = 7; read() { return this['value']; } };
            const read: any = new C().read;
            try { read.call(null); } catch (error) { console.log(error instanceof TypeError); }
            try { read.call(undefined); } catch (error) { console.log(error instanceof TypeError); }
            console.log(read.call({ value: 19 }));
            """, "true\ntrue\n19\n"];
        yield return ["lexical-arrow-retains-null", """
            const C: any = class { value = 7; make() { return () => this.value; } };
            const make: any = new C().make;
            const read: any = make.call(null);
            try { read.call({ value: 13 }); } catch (error) { console.log(error instanceof TypeError); }
            console.log(make.call({ value: 17 })());
            """, "true\n17\n"];
        yield return ["getter-uses-valid-receiver", """
            const state: any = { calls: 0, receiver: null };
            const receiver: any = {};
            Object.defineProperty(receiver, 'value', {
                get: function(this: any) { state.calls++; state.receiver = this; return 23; }
            });
            const C: any = class { value = 7; read() { return this.value; } };
            const read: any = new C().read;
            console.log(read.call(receiver), state.calls, state.receiver === receiver);
            try { read.call(null); } catch (error) { console.log(error instanceof TypeError, state.calls); }
            """, "23 1 true\ntrue 1\n"];
        yield return ["async-and-generator-contexts", """
            class C {
                value = 7;
                async read() { await Promise.resolve(0); return this.value; }
                *values() { yield this.value; }
            }
            const instance: any = new C(), read: any = instance.read, values: any = instance.values;
            async function run() {
                try { await read.call(null); } catch (error) { console.log(error instanceof TypeError); }
                try { values.call(undefined).next(); } catch (error) { console.log(error instanceof TypeError); }
                console.log(await read.call({ value: 29 }), values.call({ value: 31 }).next().value);
            }
            run();
            """, "true\ntrue\n29 31\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases()
        .SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void NonOptionalPropertyReadsRequireACoercibleReceiver(
        string name, string source, string expected, ExecutionMode mode)
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
