using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class Issue1998ArrayParityTests
{
    [Theory, ModeData]
    public void FromMapsBeforeReadingTheNextArrayElement(ExecutionMode mode)
    {
        const string source = """
            const values = [0, 1, -2, 4, -8, 16];
            const seen: any[] = [];
            const context = {calls: 0};
            const result = Array.from(values, function(value: number, index: number) {
                this.calls++;
                seen.push(value, index);
                values.splice(values.length - 1, 1);
                return 127;
            }, context);
            console.log(result.join(','), seen.join(','), context.calls);
            const growing = [1];
            console.log(Array.from(growing, function(value: number, index: number) {
                if (index === 0) growing.push(2);
                return value;
            }).join(','));
            """;
        Assert.Equal("127,127,127 0,0,1,1,-2,2 3\n1,2\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void FromReadsTheArrayIteratorGetterOnceWithOrWithoutMapping(ExecutionMode mode)
    {
        const string source = """
            const values = [1, 2];
            const iterator = values[Symbol.iterator];
            let reads = 0;
            Object.defineProperty(values, Symbol.iterator, {
                get: function() { reads++; return iterator; }, configurable: true
            });
            console.log(Array.from(values).join(','), reads);
            reads = 0;
            console.log(Array.from(values, value => value * 2).join(','), reads);
            const consumed: any = values.values();
            consumed.next();
            Object.defineProperty(values, Symbol.iterator, {
                value: function() { return consumed; }, configurable: true
            });
            console.log(Array.from(values).join(','));
            """;
        Assert.Equal("1,2 1\n2,4 1\n2\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ConcatSpreadsBooleanObjectsButKeepsBooleanPrimitives(ExecutionMode mode)
    {
        const string source = """
            const wrapped: any = new Boolean(true);
            console.log(([] as any[]).concat(wrapped)[0] === wrapped);
            wrapped[Symbol.isConcatSpreadable] = true;
            wrapped.length = 2;
            wrapped[0] = 1;
            wrapped[1] = 2;
            console.log(([] as any[]).concat(wrapped).join(','));
            const prototype: any = Boolean.prototype;
            prototype[Symbol.isConcatSpreadable] = true;
            prototype.length = 2;
            prototype[0] = 3;
            prototype[1] = 4;
            console.log(([] as any[]).concat(new Boolean(true)).join(','));
            console.log(([] as any[]).concat(true).join(','));
            """;
        Assert.Equal("true\n1,2\n3,4\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void FillAndCopyWithinDistinguishUndefinedEndFromNull(ExecutionMode mode)
    {
        const string source = """
            console.log([0, 1, 2, 3].copyWithin(0, 1, undefined).join(','));
            console.log([0, 1, 2, 3].copyWithin(0, 1, null as any).join(','));
            const undefinedFill: number[] = [0, 0];
            const nullFill: number[] = [0, 0];
            console.log(undefinedFill.fill(1, 0, undefined).join(','));
            console.log(nullFill.fill(1, 0, null as any).join(','));
            const copy: any = Array.prototype.copyWithin;
            const fill: any = Array.prototype.fill;
            const copyObject: any = {0: 0, 1: 1, 2: 2, length: 3};
            copy.call(copyObject, 0, 1, undefined);
            console.log(copyObject[0], copyObject[1], copyObject[2]);
            const fillObject: any = {0: 0, 1: 0, length: 2};
            fill.call(fillObject, 1, 0, undefined);
            console.log(fillObject[0], fillObject[1]);
            """;
        Assert.Equal("1,2,3,3\n0,1,2,3\n1,1\n0,0\n1 2 2\n1 1\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void FillCoercesRelativeIndexesBeforeClamping(ExecutionMode mode)
    {
        const string source = """
            for (const start of [undefined, null, true, false, NaN, '1', 1.5, -Infinity]) {
                const values: number[] = [0, 0];
                console.log(values.fill(1, start as any).join(','));
            }
            for (const end of [undefined, null, true, false, NaN, '1', 1.5, -Infinity]) {
                const values: number[] = [0, 0];
                console.log(values.fill(1, 0, end as any).join(','));
            }
            """;
        Assert.Equal("1,1\n1,1\n0,1\n1,1\n1,1\n0,1\n0,1\n1,1\n1,1\n0,0\n1,0\n0,0\n0,0\n1,0\n1,0\n0,0\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void FindPredicatesReceiveUndefinedOrTheExplicitStrictReceiver(ExecutionMode mode)
    {
        const string source = """
            'use strict';
            const values = [1];
            const context = {};
            let calls = 0;
            for (const name of ['find', 'findIndex', 'findLast', 'findLastIndex']) {
                (values as any)[name](function() {
                    calls++;
                    console.log(this === undefined);
                });
                (values as any)[name](function() {
                    calls++;
                    console.log(this === context);
                }, context);
                (values as any)[name](function() {
                    calls++;
                    console.log(this === null);
                }, null);
            }
            let direct: any;
            values.find(function() { calls++; direct = this; });
            console.log(direct === undefined);
            values.findIndex(function() { calls++; direct = this; }, context);
            console.log(direct === context);
            values.findLast(function() { calls++; direct = this; });
            console.log(direct === undefined);
            values.findLastIndex(function() { calls++; direct = this; }, context);
            console.log(direct === context);
            console.log(calls);
            """;
        Assert.Equal(string.Concat(Enumerable.Repeat("true\n", 16)) + "16\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ExtractedCallbackMethodsRestoreStateAfterNestedThrows(ExecutionMode mode)
    {
        const string source = """
            'use strict';
            const outer = {name: 'outer'};
            const inner = {name: 'inner'};
            const find: any = Array.prototype.find;
            const map: any = Array.prototype.map;
            const result = map.call([1, 2], function(value: number) {
                try {
                    find.call([3], function() {
                        console.log(this === inner);
                        throw 'stop';
                    }, inner);
                } catch (error) {
                    console.log(error);
                }
                console.log(this === outer);
                return value * 2;
            }, outer);
            console.log(result.join(','));
            find.call([4], function() { console.log(this === undefined); });
            """;
        Assert.Equal("true\nstop\ntrue\ntrue\nstop\ntrue\n2,4\ntrue\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void IndexedCallbacksCanNestTypedArrayMethodsAndRestoreAfterThrows(ExecutionMode mode)
    {
        const string source = """
            'use strict';
            Object.defineProperty({}, 'x', {value: 1});
            const values: any = [1, 2];
            const outer = {name: 'outer'};
            const inner = {name: 'inner'};
            console.log(values['map'](function(value: number, index: number, array: any) {
                console.log(this === outer, array === values);
                console.log([10, 20].find(function(item: number, nestedIndex: number, nestedArray: any) {
                    console.log(this === inner, nestedArray[0] === 10);
                    return item === 20;
                }, inner));
                console.log([10, 20].find(item => item === 20));
                console.log([1, 2].map(item => item + 1).find(item => item === 3));
                console.log([10, 20].reduce((total, item) => total + item, 0));
                console.log([10, 20].flatMap(function(item: number) {
                    console.log(this === inner);
                    return [item];
                }, inner).join(','));
                try {
                    [10].find(function() { throw 'stop'; }, inner);
                } catch (error) {
                    console.log(error);
                }
                console.log(this === outer, array[index] === value);
                return value;
            }, outer).join(','));
            """;
        const string iteration = "true true\ntrue true\ntrue true\n20\n20\n3\n30\ntrue\ntrue\n10,20\nstop\ntrue true\n";
        Assert.Equal(iteration + iteration + "1,2\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void ExtractedCallbacksReceiveTheBoxedPrimitiveArrayArgument(ExecutionMode mode)
    {
        const string source = """
            const map: any = Array.prototype.map;
            console.log(map.call('ab', function(value: string, index: number, array: any) {
                console.log(typeof array, array instanceof String, array.valueOf(), array[index] === value);
                return value;
            }).join(','));
            """;
        Assert.Equal("object true ab true\nobject true ab true\na,b\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void CallbackMethodsReadInheritedIndexesExposedByLengthChanges(ExecutionMode mode)
    {
        const string source = """
            Object.defineProperty(Array.prototype, '2', {
                get: function() { return 'prototype'; }, configurable: true
            });
            function input(): any {
                const values: any = [0, 1, 2];
                Object.defineProperty(values, '1', {
                    get: function() { values.length = 2; return 1; }, configurable: true
                });
                return values;
            }
            console.log(input().filter(function() { return true; }).join(','));
            console.log(input().map(function(value: any) { return value; }).join(','));
            console.log(input().some(function(value: any) { return value === 'prototype'; }));
            console.log(input().every(function(value: any) { return value !== 'prototype'; }));
            let seen: any;
            input().forEach(function(value: any, index: number) {
                if (index === 2) seen = value;
            });
            console.log(seen);
            """;
        Assert.Equal("0,1,prototype\n0,1,prototype\ntrue\nfalse\nprototype\n", TestHarness.Run(source, mode));
    }

    [Theory, ModeData]
    public void LastIndexOfReadsLiveDescriptorsAfterLengthChanges(ExecutionMode mode)
    {
        const string source = """
            const inherited: any = [0, 1, 2, 3, 4];
            Object.defineProperty(Array.prototype, '2', {
                get: function() { return 'prototype'; }, configurable: true
            });
            Object.defineProperty(inherited, '3', {
                get: function() { inherited.length = 2; return 1; }, configurable: true
            });
            console.log(inherited.lastIndexOf('prototype'));
            delete (Array.prototype as any)[2];
            const retained: any = [0, 1, 2, 3];
            Object.defineProperty(retained, '2', {
                get: function() { return 'retained'; }, configurable: false
            });
            Object.defineProperty(retained, '3', {
                get: function() { retained.length = 2; return 1; }, configurable: true
            });
            console.log(retained.lastIndexOf('retained'), retained.length);
            console.log(([undefined] as any).lastIndexOf(), [undefined].lastIndexOf(undefined));
            console.log([1, 2, 1].map(value => value).indexOf(1));
            console.log([1, 2, 1].map(value => value).lastIndexOf(1));
            """;
        Assert.Equal("2\n2 3\n0 0\n0\n2\n", TestHarness.Run(source, mode));
    }
}
