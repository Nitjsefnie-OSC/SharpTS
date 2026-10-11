using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class Issue1998ArrayFromIteratorCloseTests
{
    [Theory, ModeData]
    public void NormalForOfBreakPropagatesAReturnGetterThrow(ExecutionMode mode)
    {
        const string source = """
            const closeError = {kind: 'close'};
            const iterator: any = {
                next() { return {value: 1, done: false}; }
            };
            Object.defineProperty(iterator, 'return', {
                get: function() {
                    console.log('return', this === iterator);
                    throw closeError;
                }
            });
            const iterable: any = {
                [Symbol.iterator]() { return iterator; }
            };
            try {
                for (const value of iterable) {
                    console.log(value);
                    break;
                }
            } catch (error) { console.log(error === closeError); }
            """;
        Assert.Equal("1\nreturn true\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void MapperThrowClosesAGeneratorThroughAGuestIterator(ExecutionMode mode)
    {
        const string source = """
            let closed = 0;
            function* values() {
                try { yield 1; yield 2; }
                finally { closed++; }
            }
            const generator: any = values();
            const iterator: any = {
                next() { return generator.next(); },
                return() { return generator.return(); }
            };
            const source: any = [];
            source[Symbol.iterator] = function() { return iterator; };
            const original = {kind: 'mapper'};
            try {
                Array.from(source, function() { throw original; });
            } catch (error) { console.log(error === original); }
            console.log(closed, generator.next().done);
            """;
        Assert.Equal("true\n1 true\n", TestHarness.Run(source, mode));
    }

    [Fact]
    public void MapperThrowClosesNativeGeneratorAndLazyIteratorCarriers()
    {
        // Interpreter generator-to-enumerable adapters independently lack
        // abrupt-close forwarding. The native emitted carrier control verifies
        // that this compiler path uses the shared IteratorClose primitive.
        const string source = """
            let closed = 0;
            function* values() {
                try { yield 1; yield 2; }
                finally { closed++; }
            }
            function check(lazy: boolean) {
                closed = 0;
                const generator: any = values();
                const iterator: any = lazy ? Iterator.from(generator).map((value: any) => value * 2) : generator;
                const source: any = [];
                source[Symbol.iterator] = function() { return iterator; };
                const original = {kind: 'mapper'};
                try {
                    Array.from(source, function(value: any) {
                        console.log(value);
                        throw original;
                    });
                } catch (error) { console.log(error === original); }
                console.log(closed, iterator.next().done);
            }
            check(false);
            check(true);
            """;
        Assert.Equal("1\ntrue\n1 true\n2\ntrue\n1 true\n", TestHarness.RunCompiled(source));
    }

    [Theory, ModeData]
    public void MapperThrowPreservesItsValueWhenReturnLookupOrCallFails(ExecutionMode mode)
    {
        const string source = """
            function check(kind: number) {
                const original = {kind: 'mapper'};
                const closeError = {kind: 'close'};
                const iterator: any = {
                    next() { return {value: 1, done: false}; }
                };
                Object.defineProperty(iterator, 'return', {
                    get: function() {
                        console.log('lookup', this === iterator);
                        if (kind === 0) throw closeError;
                        return function() {
                            console.log('close', this === iterator);
                            if (kind === 1) throw closeError;
                            return undefined;
                        };
                    }
                });
                const source: any = [];
                source[Symbol.iterator] = function() { return iterator; };
                const context = {kind: 'receiver'};
                try {
                    Array.from(source, function() {
                        console.log('mapper', this === context);
                        throw original;
                    }, context);
                } catch (error) { console.log('caught', error === original); }
            }
            check(0);
            check(1);
            check(2);
            """;
        Assert.Equal("mapper true\nlookup true\ncaught true\n"
            + "mapper true\nlookup true\nclose true\ncaught true\n"
            + "mapper true\nlookup true\nclose true\ncaught true\n",
            TestHarness.Run(source, mode));
    }
}
