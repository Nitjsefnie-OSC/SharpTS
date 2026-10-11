using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class Issue2000ArrayFromTests
{
    [Theory, CompiledOnlyData]
    public void ThrowingMapperClosesGeneratorReturnedByIteratorMethod(ExecutionMode mode)
    {
        const string source = """
            let closed = 0;
            let resumed = 0;
            function* values() {
                try {
                    yield 1;
                    resumed++;
                    yield 2;
                } finally { closed++; }
            }
            const iterator: any = values();
            const source: any = { [Symbol.iterator]() { return iterator; } };
            const original = new Error('mapper');
            try { Array.from(source, () => { throw original; }); }
            catch (error) { console.log(error === original); }
            console.log(closed, resumed, iterator.next().done);
            """;

        Assert.Equal("true\n1 0 true\n", TestHarness.Run(source, mode));
    }

    [Theory, CompiledOnlyData]
    public void GeneratorCloseFailurePreservesMapperException(ExecutionMode mode)
    {
        const string source = """
            let closed = 0;
            function* values() {
                try { yield 1; yield 2; }
                finally { closed++; throw new Error('close'); }
            }
            const iterator: any = values();
            const source: any = { [Symbol.iterator]() { return iterator; } };
            const original = new Error('mapper');
            try { Array.from(source, () => { throw original; }); }
            catch (error) { console.log(error === original); }
            console.log(closed, iterator.next().done);
            """;

        Assert.Equal("true\n1 true\n", TestHarness.Run(source, mode));
    }

    [Theory, CompiledOnlyData]
    public void GuestCloseFailuresPreserveMapperException(ExecutionMode mode)
    {
        const string source = """
            let closed = 0;
            const original = new Error('mapper');
            for (const kind of ['getter', 'call', 'primitive']) {
                const iterator: any = { next() { return { value: 1, done: false }; } };
                Object.defineProperty(iterator, 'return', {
                    get() {
                        closed++;
                        if (kind === 'getter') throw new Error('get return');
                        return function() {
                            console.log(this === iterator);
                            if (kind === 'call') throw new Error('call return');
                            return 0;
                        };
                    }
                });
                const source: any = { [Symbol.iterator]() { return iterator; } };
                try { Array.from(source, () => { throw original; }); }
                catch (error) { console.log(error === original); }
            }
            console.log(closed);
            """;

        Assert.Equal("true\ntrue\ntrue\ntrue\ntrue\n3\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void OmittedAndUndefinedMapperConsumeEachIteratorValueOnce(ExecutionMode mode)
    {
        const string source = """
            let iteratorCalls = 0;
            let valueReads = 0;
            let closed = 0;
            function makeSource(): any {
                return {
                    [Symbol.iterator]() {
                        iteratorCalls++;
                        let index = 0;
                        return {
                            next() {
                                index++;
                                return index > 2 ? { done: true } : {
                                    done: false,
                                    get value() { valueReads++; return index === 1 ? undefined : 3; }
                                };
                            },
                            return() { closed++; return {}; }
                        };
                    }
                };
            }
            const from: any = Array.from;
            const omitted = from(makeSource());
            const explicit = from(makeSource(), undefined);
            console.log(omitted.length, omitted[0] === undefined, omitted[1]);
            console.log(explicit.length, explicit[0] === undefined, explicit[1]);
            console.log(iteratorCalls, valueReads, closed);
            """;

        Assert.Equal("2 true 3\n2 true 3\n2 4 0\n", TestHarness.Run(source, mode));
    }
}
